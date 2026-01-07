using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yggdrasil.Logging;

namespace Kakia.TW.Shared.Util
{
	public static class TaskHelper
	{
		/// <summary>
		/// Executes the given task and catches and logs any exceptions that may occur.
		/// </summary>
		/// <param name="task"></param>
		public static void CallSafe(Task task)
		{
			task.ContinueWith(t =>
			{
				if (t.Exception == null)
					return;

				Log.Error("An exception occured during an asynchronous operation: {0}", t.Exception);
			});
		}
	}
}
