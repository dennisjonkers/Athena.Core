using System;

namespace Athena.Core
{
	/// <summary>
	/// Insert duplicate key error
	/// </summary>
	public class ExistsException : Exception
	{

		public ExistsException() : base()
		{
			
		}

		public ExistsException(string message) : base(message)
		{
		}

	}

}
