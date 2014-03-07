using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace imgsqz
{
	public static class ConsoleLogger
	{
		private static object LOCK_consoleWriteLock = new object();
		private static string _spinnerChars = @"/-\|";
		private static int _spinnerCharIndex = 0;

		public static void WriteLine(string format = null)
		{
			Console.WriteLine(format);
		}

		public static void WriteLine(string format, params object[] args)
		{
			if (args != null && args.Length > 0)
				format = string.Format(format, args); //only call string format if we have args - else it'll throw an exception
			WriteLine(format);
		}

		public static void WriteLine(Exception ex)
		{
			Console.WriteLine(ex);
		}

		public static void WriteAt(object s, int x = 0, int y = 0)
		{
			Thread.Sleep(1000);
			lock (LOCK_consoleWriteLock)
			{
				int origRow = Console.CursorTop;
				int origCol = Console.CursorLeft;

				try
				{
					Console.SetCursorPosition(origCol + x, origRow + y);
					Console.Write(s);
				}
				catch (ArgumentOutOfRangeException e)
				{
					Console.WriteLine("WriteAt({0},{1}) ERROR: {2}", x, y, e.Message);
				}
				finally
				{
					Console.SetCursorPosition(origCol, origRow);
				}
			}
		}

		public static void WriteNextSpinnerChar()
		{
			_spinnerCharIndex++;
			if (_spinnerCharIndex >= _spinnerChars.Length)
				_spinnerCharIndex = 0;
			WriteAt(_spinnerChars[_spinnerCharIndex]);
		}


	}
}
