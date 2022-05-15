/*
 * Copyright 2022 Marcel Bulla
 * 
 * This file is part of Debugger-GUI.
 * 
 * Debugger-GUI is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version.
 * 
 * Foobar is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more
 * details.
 * 
 * You should have received a copy of the GNU General Public License along
 * with Foobar. If not, see http://www.gnu.org/licenses/.
 */

using System;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Debugger.Source.ViewModel;

namespace Debugger.Source.Connector
{
    /// <summary>
    /// This class connects the app with another console process and manages console I/O.
    /// Both In- and Output can be bound to a GUI.
    /// </summary>
    public class ProcessConnector : BaseClass
    {
        #region Fields

        private Thread _threadStdOut;
        private Thread _threadStdErr;

        #endregion

        #region Properties

        /// <summary>
        /// The process we are connected with.
        /// It must be created by us, otherwise console I/O will not work.
        /// </summary>
        public Process TargetProcess
        {
            get { return GetValue<Process>(nameof(TargetProcess)); }
            private set { SetValue(nameof(TargetProcess), value); }
        }

        /// <summary>
        /// A list of all lines written to STDOUT of the target process.
        /// The last line can change dynamically while it is still written.
        /// </summary>
        public BindingList<string> StdOut
        {
            get { return GetValue<BindingList<string>>(nameof(StdOut)); }
            private set { SetValue(nameof(StdOut), value); }
        }

        /// <summary>
        /// A list of all line written to STDERR of the target process.
        /// </summary>
        public BindingList<string> StdErr
        {
            get { return GetValue<BindingList<string>>(nameof(StdErr)); }
            private set { SetValue(nameof(StdErr), value); }
        }

        /// <summary>
        /// Convenience access to the last line, or current line being written to STDOUT.
        /// </summary>
        public string CurrentStdOut => StdOut[^1];

        /// <summary>
        /// Convenience access to the last line being written to STDERR.
        /// </summary>
        public string CurrentStdErr => StdErr[^1];

        /// <summary>
        /// A string that can be written to STDIN of the target process.
        /// </summary>
        public string Input
        {
            get { return GetValue<string>(nameof(Input)); }
            set { SetValue(nameof(Input), value); }
        }

        /// <summary>
        /// Command to write the input string to STDIN of the target process.
        /// Intended for binding to a GUI element.
        /// </summary>
        public RelayCommand EnterInputCommand
        {
            get { return GetValue<RelayCommand>(nameof(EnterInputCommand)); }
            private set { SetValue(nameof(EnterInputCommand), value); }
        }

        #endregion

        #region Constructor

        public ProcessConnector()
        {
            TargetProcess = new Process();

            StdOut = new BindingList<string>();
            StdErr = new BindingList<string>();

            StdOut.Add("");
            StdErr.Add("");

            EnterInputCommand = new RelayCommand(cmd => WriteInput());

            _threadStdOut = null;
            _threadStdErr = null;

            App.Instance.Exit += Instance_Exit;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Launches a process with the given executable and connects its I/O.
        /// </summary>
        /// <param name="strPath">The path to the executable</param>
        /// <param name="strArgs">Additional command line arguments</param>
        public void StartProcess(string strPath, string strArgs = "")
        {
            TargetProcess.StartInfo.FileName = strPath;
            TargetProcess.StartInfo.Arguments = strArgs;
            TargetProcess.StartInfo.RedirectStandardError = true;
            TargetProcess.StartInfo.RedirectStandardOutput = true;
            TargetProcess.StartInfo.RedirectStandardInput = true;
            TargetProcess.StartInfo.UseShellExecute = false;
            TargetProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(strPath);
            TargetProcess.StartInfo.CreateNoWindow = true;

            TargetProcess.Start();

            _threadStdOut = new Thread(() =>
            {
                while (!TargetProcess.HasExited)
                {
                    AppendOutput(StdOut, CurrentStdOut + (char)TargetProcess.StandardOutput.Read(), false);
                }
            });
            _threadStdOut.Start();

            _threadStdErr = new Thread(() =>
            {
                while (!TargetProcess.HasExited)
                {
                    AppendOutput(StdErr, CurrentStdErr + TargetProcess.StandardError.ReadLine() + "\n", true);
                }
            });
            _threadStdErr.Start();
        }

        /// <summary>
        /// Stops the process.
        /// </summary>
        public void StopProcess()
        {
            TargetProcess.Kill();
            _threadStdOut?.Join();
            _threadStdErr?.Join();
        }

        /// <summary>
        /// Writes the Input property to STDIN of the target process.
        /// </summary>
        public void WriteInput()
        {
            if (TargetProcess != null && !TargetProcess.HasExited)
            {
                TargetProcess.StandardInput.WriteLine(Input);
                Input = "";
            }
        }

        #endregion

        #region protected Methods

        /// <summary>
        /// Decide if this output should be filtered.
        /// Can be overidden.
        /// </summary>
        /// <param name="strContent">The content to be tested for filtering</param>
        /// <returns>True if the content should be filtered (NOT displayed), otherwise false.</returns>
        protected virtual bool ApplyFilter(string strContent)
        {
            return false;
        }

        /// <summary>
        /// Called when the console output of the target process has changed.
        /// </summary>
        /// <param name="strLastLine">The current output</param>
        /// <param name="bUnfinished">True, if the line is still being outputted and can change, otherwise false.</param>
        /// <param name="bIsError">True, if this is an STDERR output, otherwise false.</param>
        protected virtual void OutputChanged(string strLastLine, bool bUnfinished, bool bIsError)
        {

        }

        #endregion

        private void AppendOutput(BindingList<string> listTarget, string strContent, bool bIsError)
        {
            if (strContent.Contains('\n'))
            {
                var arrNext = strContent.Split('\n', StringSplitOptions.None);
                bool bApplied = false;

                for (int i = 0; i < arrNext.Length; i++)
                {
                    if (ApplyFilter(arrNext[i]))
                    {
                        continue;
                    }
                    DispatchService.Invoke(() =>
                    {
                        if (!bApplied)
                        {
                            listTarget[^1] = arrNext[i];
                            bApplied = true;
                            OutputChanged(arrNext[i], false, bIsError);
                        }
                        else
                        {
                            listTarget.Add(arrNext[i]);
                            OutputChanged(arrNext[i], i == arrNext.Length - 1, bIsError);
                        }

                    });
                }
            }
            else
            {
                DispatchService.Invoke(() =>
                {
                    if (ApplyFilter(strContent))
                    {
                        listTarget[^1] = "";
                    }
                    else
                    {
                        listTarget[^1] = strContent;
                    }
                    OutputChanged(listTarget[^1], true, bIsError);
                });
            }
        }

        private void Instance_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            StopProcess();
        }
    }
}