(* Copyright 2014 FSharpN00b.
This file is part of Tagger.

Tagger is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Tagger is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Tagger.  If not, see <http://www.gnu.org/licenses/>.

Tagger uses AvalonEdit, which is copyright 2014 AlphaSierraPapa for the SharpDevelop Team under the terms of the MIT License. For more information see www.avalonedit.net. *)

module Test

(* 
References: PresentationCore, ""Framework, System.Xaml, WindowsBase. We needed these to use types such as FileTreeView.
In Project properties, Build tab, configuration Active (Debug), I added 0058 to the suppress warnings list. The compiler refused to stop warning about the indentation of the test lists, no matter how I indented them.
*)

// Directory, File
open System.IO
// MessageBox
open System.Windows

open Xunit

// LoggerWrapper
open LoggerWrapper
open TestHelpers
open ListHelpersTest
open ConfigTest
open FileTreeViewTest
open FileTreeViewControllerTest
open TabControlTest
open EditorTest
open PaneControllerTest
open TagControllerTest
open MainControllerTest
open ProjectTest
open MarginTest
open AddOnServerTest
open AddOnServerControllerTest
// Logger
open Logger

[<Fact>]
let Test_() =
(* We need to create the Logger in the Test_ method, because that creates it new every time the test is run. If we put it in TestHelpers.fs, the same Logger remains loaded in xUnit.Net along with the assembly. Then, when we try to run the test twice, we get an error about writing to a closed TextWriter (which is what's used by Logger). To fix this, we have to either unload and then reload the assembly in xUnit.Net, or else re-create the Logger each time in the Test_ method. *)
(* Replace the Tagger logger with our own, so we can control where the log file is. Use the Tagger log config file and replace the log file location, so we don't have to duplicate the entire log config file.*)
    do
        _logger.Logger <- new Logger (log_config, LogConfigFormat.String) |> Some
(* According to the log config, the Logger should have one output string named "default". The Logger wrapper used by the Tagger application doesn't expose the output string map, so we have to access the internal Logger first. *)
        _log_string <- (_logger.Logger.Value.GetOutputString "default").Value

    let test_groups = [
        new ListHelpersTest () :> ITestGroup;
        new ConfigTest () :> ITestGroup;
        new FileTreeViewTest () :> ITestGroup;
        new FileTreeViewControllerTest () :> ITestGroup;
        new TabControlTest () :> ITestGroup;
        new EditorTest () :> ITestGroup;
        new PaneControllerTest () :> ITestGroup;
        new TagControllerTest () :> ITestGroup;
        new MainControllerTest () :> ITestGroup;
        new ProjectTest () :> ITestGroup;
        new AddOnServerTest () :> ITestGroup;
        new AddOnServerControllerTest () :> ITestGroup;
        ]

    try do
        test_groups |> List.map (fun m -> m.tests_log) |> List.concat |> run_tests_that_log
        test_groups |> List.map (fun m -> m.tests_throw) |> List.concat |> run_tests_that_throw
        test_groups |> List.map (fun m -> m.tests_no_log) |> List.concat |> run_tests_that_do_not_log
    finally do
(* Dispose of the Logger's resources. *)
        _logger.Dispose ()
(* Warn the user that the log file is about to be deleted. *)
//        "All files are about to be cleaned up. Inspect them before clicking OK." |> MessageBox.Show |> ignore
(* Clean up files and directories. *)
        CleanUp ()
