using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

namespace OpenIDE.Core.Language
{
	public class PluginLocator
	{
		private string[] _enabledLanguages;
		private string _languageRoot;
		private Action<string> _dispatchMessage;
		private List<LanguagePlugin> _plugins = new List<LanguagePlugin>();

		public PluginLocator(string[] enabledLanguages, string languageRoot, Action<string> dispatchMessage)
		{
			_enabledLanguages = enabledLanguages;
			_languageRoot = languageRoot;
			_dispatchMessage = dispatchMessage;
		}

		public LanguagePlugin[] Locate()
		{
			return getPlugins()
				.Select(x => getPlugin(x))
				.Where(x => _enabledLanguages == null || _enabledLanguages.Contains(x.GetLanguage()))
				.ToArray();
		}

		private LanguagePlugin getPlugin(string name) {
			var plugin = _plugins.FirstOrDefault(x => x.FullPath == name);
			if (plugin == null) {
				plugin = new LanguagePlugin(name, _dispatchMessage);
				_plugins.Add(plugin);
			}
			return plugin;
		}
		
		public IEnumerable<BaseCommandHandlerParameter> GetUsages()
		{
			var commands = new List<BaseCommandHandlerParameter>();
			Locate().ToList()
				.ForEach(plugin => 
					{
						plugin.GetUsages().ToList()
							.ForEach(y => commands.Add(y));
					});
			return commands;
		}

		private string[] getPlugins()
		{
			var dir = 
				Path.Combine(
					_languageRoot,
					"Languages");
			if (!Directory.Exists(dir))
				return new string[] {};
			return Directory.GetFiles(dir);
		}
	}
}
