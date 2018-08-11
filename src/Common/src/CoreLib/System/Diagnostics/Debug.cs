// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Do not remove this, it is needed to retain calls to these conditional methods in release builds
#define DEBUG
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace System.Diagnostics
{

    // This is not a user-facing extension point. It is designed for the framework owners to have more flexibility around layering and dependencies. We can build both default behavior and extensibility points for 3rd party devs (such as TraceListener)
    // as different implementations of this interface.
    //
    // I marked it public but I assume it get exposed to the Trace assembly via a private contract.
    public interface IDebugImpl
    {
        void Close();
        void Flush();
        void Write(string message);
        void WriteLine(string message);
        void Assert(bool condition, string message, string detailMessage);
        //incomplete, there are bunch more overloads of Write/WriteLine/WriteLineIf etc
    }

    // This implementation of IDebugImpl mimics the behavior of DefaultTraceListener in its default configuration. It might be useful for scenarios where we prune the entire Trace library because the app code never called any Trace APIs.
    internal class MinimalDebugImpl : IDebugImpl
    {
        private static readonly object s_lock = new object();
        private static bool s_needIndent = true;

        public void Close() { }
        public void Flush() { }
        public void Write(string message)
        {
            lock (s_lock)
            {
                if (message == null)
                {
                    Debug.s_WriteCore(string.Empty);
                    return;
                }
                if (s_needIndent)
                {
                    message = GetIndentString() + message;
                    s_needIndent = false;
                }
                Debug.s_WriteCore(message);
                if (message.EndsWith(Environment.NewLine))
                {
                    s_needIndent = true;
                }
            }
        }

        public void WriteLine(string message)
        {
            Write(message + Environment.NewLine);
        }

        public void Assert(bool condition, string message, string detailMessage)
        {
            if (!condition)
            {
                string stackTrace;
                try
                {
                    stackTrace = new StackTrace(0, true).ToString(System.Diagnostics.StackTrace.TraceFormat.Normal);
                }
                catch
                {
                    stackTrace = "";
                }
                WriteLine(Debug.FormatAssert(stackTrace, message, detailMessage));
                Debug.ShowDialog(stackTrace, message, detailMessage, "Assertion Failed");
            }
        }
    }

    /// <summary>
    /// Provides a set of properties and methods for debugging code.
    /// </summary>
    public static partial class Debug
    {
        private static IDebugImpl _impl;
        private static IDebugImpl Impl
        {
            get
            {
                if(_impl == null)
                {
                    IDebugImpl tempImpl = null;
                    try
                    {
                        Type implType = Type.GetType(
                            "System.Diagnostics.TraceSource, System.Diagnostics.TraceDebugImpl, Version=4.0.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                            throwOnError: false);

                        if (implType != null)
                        {
                            tempImpl = Activator.CreateInstance(symbolsType);
                        }
                    }
                    catch (Exception e) { } //replace with something more precise

                    if(implType == null)
                    {
                        tempImpl = new MinimalDebugImpl();
                    }

                    Interlocked.CompareExchange(ref _impl, tempImpl, null);
                }
                return _impl;
            }
        }

        public static bool AutoFlush { get { return true; } set { } }

        [ThreadStatic]
        private static int s_indentLevel;
        public static int IndentLevel
        {
            get
            {
                return s_indentLevel;
            }
            set
            {
                s_indentLevel = value < 0 ? 0 : value;
            }
        }

        private static int s_indentSize = 4;
        public static int IndentSize
        {
            get
            {
                return s_indentSize;
            }
            set
            {
                s_indentSize = value < 0 ? 0 : value;
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Close() { Impl.Close(); }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Flush() { Impl.Flush(); }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Indent()
        {
            IndentLevel++;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Unindent()
        {
            IndentLevel--;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Print(string message)
        {
            Write(message);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Print(string format, params object[] args)
        {
            Write(string.Format(null, format, args));
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Assert(bool condition)
        {
            Assert(condition, string.Empty, string.Empty);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Assert(bool condition, string message)
        {
            Assert(condition, message, string.Empty);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Assert(bool condition, string message, string detailMessage)
        {
            Impl.Assert(condition, message, detailMessage);
        }

        internal static void ContractFailure(bool condition, string message, string detailMessage, string failureKindMessage)
        {
            if (!condition)
            {
                string stackTrace;
                try
                {
                    stackTrace = new StackTrace(2, true).ToString(System.Diagnostics.StackTrace.TraceFormat.Normal);
                }
                catch
                {
                    stackTrace = "";
                }
                WriteLine(FormatAssert(stackTrace, message, detailMessage));
                s_ShowDialog(stackTrace, message, detailMessage, SR.GetResourceString(failureKindMessage));
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Fail(string message)
        {
            Assert(false, message, string.Empty);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Fail(string message, string detailMessage)
        {
            Assert(false, message, detailMessage);
        }

        private static string FormatAssert(string stackTrace, string message, string detailMessage)
        {
            string newLine = GetIndentString() + Environment.NewLine;
            return SR.DebugAssertBanner + newLine
                   + SR.DebugAssertShortMessage + newLine
                   + message + newLine
                   + SR.DebugAssertLongMessage + newLine
                   + detailMessage + newLine
                   + stackTrace;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Assert(bool condition, string message, string detailMessageFormat, params object[] args)
        {
            Assert(condition, message, string.Format(detailMessageFormat, args));
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLine(string message)
        {
            Impl.WriteLine(message);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Write(string message)
        {
            Impl.Write(message);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLine(object value)
        {
            WriteLine(value?.ToString());
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLine(object value, string category)
        {
            WriteLine(value?.ToString(), category);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(null, format, args));
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLine(string message, string category)
        {
            if (category == null)
            {
                WriteLine(message);
            }
            else
            {
                WriteLine(category + ":" + message);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Write(object value)
        {
            Write(value?.ToString());
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Write(string message, string category)
        {
            if (category == null)
            {
                Write(message);
            }
            else
            {
                Write(category + ":" + message);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Write(object value, string category)
        {
            Write(value?.ToString(), category);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteIf(bool condition, string message)
        {
            if (condition)
            {
                Write(message);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteIf(bool condition, object value)
        {
            if (condition)
            {
                Write(value);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteIf(bool condition, string message, string category)
        {
            if (condition)
            {
                Write(message, category);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteIf(bool condition, object value, string category)
        {
            if (condition)
            {
                Write(value, category);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLineIf(bool condition, object value)
        {
            if (condition)
            {
                WriteLine(value);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLineIf(bool condition, object value, string category)
        {
            if (condition)
            {
                WriteLine(value, category);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLineIf(bool condition, string message)
        {
            if (condition)
            {
                WriteLine(message);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void WriteLineIf(bool condition, string message, string category)
        {
            if (condition)
            {
                WriteLine(message, category);
            }
        }



        private static string s_indentString;

        private static string GetIndentString()
        {
            int indentCount = IndentSize * IndentLevel;
            if (s_indentString?.Length == indentCount)
            {
                return s_indentString;
            }
            return s_indentString = new string(' ', indentCount);
        }

        private sealed class DebugAssertException : Exception
        {
            internal DebugAssertException(string stackTrace) :
                base(Environment.NewLine + stackTrace)
            {
            }

            internal DebugAssertException(string message, string stackTrace) :
                base(message + Environment.NewLine + Environment.NewLine + stackTrace)
            {
            }

            internal DebugAssertException(string message, string detailMessage, string stackTrace) :
                base(message + Environment.NewLine + detailMessage + Environment.NewLine + Environment.NewLine + stackTrace)
            {
            }
        }

        // This is now a public extensibility point, roughly equivalent to the AssertionFailed event handler that was first proposed. It is called by both the full implementation of DefaultTraceListener
        // as well as the MinimalDebugImpl.Assert. This allows a UI customization mechanism that doesn't require the Trace library to be loaded, but it still works if it is.
        // It honors the legacy customization approaches shown in most samples because if the DefaultTraceListener is removed from the collection or DefaultTraceListner.AssertUIEnabled is set to false
        // then this won't get called.
        //
        // With a little more refactoring it could be almost exactly the AssertionFailed proposal. For the most part I don't have a strong position on the differences either way:
        // The non-UI behavior isn't configurable (without tweaking the TraceListeners), we paid the cost of formatting a stack trace up-front, I made this a singleton delegate rather than an event
        //
        // I do think we should respect DefaultTraceListener.AssertUIEnabled via some mechanism. Making the action only include the dialog itself was a simple way to do that but there are others.
        public static Action<string, string, string, string> ShowAssertDialog = ShowDialog;

        internal static Action<string> s_WriteCore = WriteCore;
    }
}
