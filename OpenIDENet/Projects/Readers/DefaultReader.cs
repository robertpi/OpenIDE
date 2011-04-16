using System;
using OpenIDENet.Versioning;
using System.IO;
using OpenIDENet.FileSystem;
using System.Xml;
namespace OpenIDENet.Projects.Readers
{
	public class DefaultReader : IReadProjectsFor
	{
		private IFS _fs;
		private XmlNamespaceManager _nsManager = null;
		
		public DefaultReader(IFS fs)
		{
			_fs = fs;
		}
		
		public bool SupportsProject<T>() where T : IAmProjectVersion
		{
			return typeof(T).Equals(typeof(VS2010));
		}
		
		public IProject Read(string fullPath)
		{
			var content = _fs.ReadFileAsText(fullPath);
			return new Project(Path.GetFullPath(fullPath), content, getSettings(content));
		}
		
		private ProjectSettings getSettings(string content)
		{
			var ns = "ns";
			var document = new XmlDocument();
			if (tryOpen(document, content))
			{
				var node = document.SelectSingleNode("b:Project/b:PropertyGroup/b:RootNamespace", _nsManager);
				if (node != null)
					ns = node.InnerText;
			}
			
			return new ProjectSettings(ProjectType.CSharp, ns);
		}
		
		private bool tryOpen(XmlDocument document, string xml)
		{
			try
			{
				document.LoadXml(xml);
				if (xml.Contains("http://schemas.microsoft.com/developer/msbuild/2003"))
				{
					_nsManager = new XmlNamespaceManager(document.NameTable);
					_nsManager.AddNamespace("b", "http://schemas.microsoft.com/developer/msbuild/2003");
				}
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}

