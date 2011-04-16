using System;
using System.Collections.Generic;
using OpenIDENet.Versioning;
namespace OpenIDENet.Projects
{
	public interface IProject
	{
		string Fullpath { get; }
		object Content { get; }
		bool IsModified { get; }
		
		ProjectSettings Settings { get; }
		
		void SetContent(object content);
	}
}

