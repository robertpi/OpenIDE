using System;
using System.Xml;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using OpenIDENet.Projects;
using OpenIDENet.EditorEngineIntegration;
using OpenIDENet.Languages;

namespace OpenIDENet.Arguments.Handlers
{
	class CreateHandler : ICommandHandler
	{
		private OpenIDENet.Files.IResolveFileTypes _fileTypeResolver;
		private ILocateEditorEngine _editorFactory; 
		
		public CommandHandlerParameter Usage {
			get {
				try {
					var usage = new CommandHandlerParameter(
						SupportedLanguage.All,
						CommandType.ProjectCommand,
						Command,
						"Uses the create template to create what ever project related specified by the template");
				
					getTemplates().ToList()
						.ForEach(x => 
							{
								var command = getUsage(x);
								if (command != null)
									usage.Add(command);
							});
					return usage;
				} catch {
					return null;
				}
			}
		}
		
		private BaseCommandHandlerParameter getUsage(string template)
		{
			var name = Path.GetFileNameWithoutExtension(template);
			var definition = new CreateTemplate(template, null).GetUsageDefinition();
			var parser = new TemplateDefinitionParser();
			var usage = parser.Parse(name, definition);
			if (usage == null)
				return null;
			var fileParam = new BaseCommandHandlerParameter("ITEM_NAME", "The name of the Project/Item to create");
			usage.Parameters.ToList()
				.ForEach(x => fileParam.Add(x));
			usage = new BaseCommandHandlerParameter(usage.Name, usage.Description);
			usage.Add(fileParam);
			return usage;
		}

		public string Command { get { return "create"; } }

		public CreateHandler(
			OpenIDENet.Files.IResolveFileTypes fileTypeResolver,
			ILocateEditorEngine editorFactory)
		{
			_fileTypeResolver = fileTypeResolver;
			_editorFactory = editorFactory;
		}
		
		public void Execute(string[] arguments, Func<string, ProviderSettings> getTypesProvider)
		{
			if (arguments.Length < 2)
			{
				Console.WriteLine("Invalid number of arguments. " +
					"Usage: create {template name} {item name} {template arguments}");
				return;
			}
			
			var project = getFile(arguments[1]);
			var template = pickTemplate(arguments[0]);
			if (template == null)
				return;
			
			template.Run(project, getArguments(arguments));

			Console.WriteLine("Created {0}", project);

			if (template.File == null)
				return;
			gotoFile(template.File.Fullpath, template.Line, template.Column, Path.GetDirectoryName(project));
		}
		
		private ICreateTemplate pickTemplate(string templateName)
		{
			var template = getTemplates().FirstOrDefault(x => x.Equals("{0}." + templateName));
			if (template == null)
				return null;
			return new CreateTemplate(template, _fileTypeResolver);
		}

		private IEnumerable<string> getTemplates()
		{
			var templateDir = 
				System.IO.Path.Combine(
					System.IO.Path.Combine(
						System.IO.Path.Combine(
							System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
						"templates"),
					"default"), "create");
				return System.IO.Directory.GetFiles(templateDir);
		}

		private string getFile(string argument)
		{
			var filename = Path.GetFileName(argument);
			var dir = Path.GetDirectoryName(argument).Trim();
			if (dir.Length == 0)
				return Path.Combine(Environment.CurrentDirectory, filename);
			if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, dir)))
				return Path.Combine(
					Path.Combine(Environment.CurrentDirectory, dir),
					filename);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			return Path.Combine(dir, filename);
		}

		private string[] getArguments(string[] args)
		{
			if (args.Length == 1)
				return new string[] {};
			string[] newArgs = new string[args.Length - 1];
			for (int i = 1; i < args.Length; i++)
				newArgs[i - 1] = args[i];
			return newArgs;
		}

		private void gotoFile(string file, int line, int column, string location)
		{
			var instance = _editorFactory.GetInstance(location);
			if (instance == null)
				return;
			instance.GoTo(file, line, column);
			instance.SetFocus();
		}	
	}

	public interface ICreateTemplate
	{
		OpenIDENet.Files.IFile File { get; }
		int Line { get; }
		int Column { get; }
		
		void Run(string projectPath, string[] arguments);
	}
	
	class CreateTemplate : ICreateTemplate
	{
		private string _file;
		private OpenIDENet.Files.IResolveFileTypes _typeResolver;
		
		public OpenIDENet.Files.IFile File { get; private set; }
		public int Line { get; private set; }
		public int Column { get; private set; }
		
		public CreateTemplate(string file, OpenIDENet.Files.IResolveFileTypes typeResolver)
		{
			File = null;
			_file = file;
			_typeResolver = typeResolver;
		}
		
		public string GetUsageDefinition()
		{
			return run("get_definition");
		}
		
		public void Run(string projectPath, string[] arguments)
		{
			try
			{
				var xml = getXml(projectPath, arguments);
				var tempFile = System.IO.Path.GetTempFileName();
				System.IO.File.WriteAllText(tempFile, xml);
				run(
					string.Format("\"{0}\" \"{1}\"",
						projectPath,
						tempFile));
				System.IO.File.Delete(tempFile);
				getFile();
				getPositionInfo();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				File = null;
			}
		}
		
		private string getXml(
			string projectPath,
			string[] arguments)
		{
			var sb = new StringBuilder();
			using (var writer = XmlWriter.Create(sb))
			{
				writer.WriteStartDocument();
				writer.WriteStartElement("parameters");
					writer.WriteElementString("file", projectPath);
					writer.WriteStartElement("custom_parameters");
						arguments.ToList().ForEach(x => writer.WriteElementString("parameter", x));
					writer.WriteEndElement();
				writer.WriteEndElement();
			}
			return sb.ToString();
		}
		
		private void getFile()
		{
			try
			{
				var file = run("get_file");
				File = _typeResolver.Resolve(file);
			}
			catch
			{
				File = null;
			}
		}

		private void getPositionInfo()
		{
			try
			{
				var positionString = run("get_position")
					.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
				Line = int.Parse(positionString[0]);
				Column = int.Parse(positionString[1]);
			}
			catch
			{
				Line = 0;
				Column = 0;
			}
		}
		
		private string run(string arguments)
		{
			var proc = new Process();
			proc.StartInfo = new ProcessStartInfo(_file, arguments);
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
			proc.Start();
			var output = proc.StandardOutput.ReadToEnd();
			proc.WaitForExit();
			if (output.Length > Environment.NewLine.Length)
				return output.Substring(0, output.Length - Environment.NewLine.Length);
			return output;
		}
	}
}