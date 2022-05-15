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
using System.Windows;
using System.Windows.Threading;

namespace Debugger.Source.ViewModel
{
    /// <summary>
    /// Global access to the UI dispatcher
    /// </summary>
    public static class DispatchService
    {
        /// <summary>
        /// Invokes code into the UI thread.
        /// </summary>
        /// <param name="action">The code</param>
        public static void Invoke(Action action)
        {
            Dispatcher dispatchObject = Application.Current?.Dispatcher;
            try
            {
                dispatchObject?.Invoke(action);
            }
            catch { }
        }
    }
}
