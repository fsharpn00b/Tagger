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

module TestHelpers

// Type
open System
// Directory, File
open System.IO
// StringBuilder
open System.Text
// Regex
open System.Text.RegularExpressions

open Xunit

(* Types used in unit testing. *)
//#region
/// <summary>A test that is expected to throw an exception. </summary>
type TestThatThrows = {
    part : string;
    name : string;
    exception_type : Type;
    test : string -> unit;
}

/// <summary>A test that is not expected to throw an exception. </summary>
type TestThatDoesNotThrow = {
    part : string;
    name : string;
    test : string -> unit;
}

type ITestGroup = interface
    abstract tests_log : TestThatDoesNotThrow list with get
    abstract tests_throw : TestThatThrows list with get
    abstract tests_no_log : TestThatDoesNotThrow list with get
end
//#endregion

(* Values used in testing. *)
//#region
(* The current directory. *)
let current_dir = Directory.GetCurrentDirectory ()

(* A log string that we'll extract from the logger and use to make sure each event is being logged correctly. *)
let mutable (_log_string : StringBuilder) = null

(* The log configuration string. We don't instantiate the Logger here. For an explanation, see Test.fs. *)
let log_config =
(* The current assembly is in Tagger/Test/bin/debug, and the log configuration file is in the Tagger project. *)
    let log_config_ = File.ReadAllText @"..\..\..\Tagger\logconfig.xml"
(* Many events log to a file and open a message box. We want to replace this pair of actions for testing purposes. .*? means to match anything, not including the next part of the pattern; i.e. to be non-greedy. *)
    let match_ = @"<actions>.*?</actions>"
(* Replace unwanted actions with one that logs to a string named "default" and adds a newline to the event message. *)
    let replace = @"<actions><WriteToString name=""default"" newline=""true"" /></actions>"
(* In Regex, the . matches every character except \n. To match that too, we must use single-line mode.
http://stackoverflow.com/questions/3034535/how-do-i-specify-a-wildcard-for-any-character-in-a-c-sharp-regex-statement
http://msdn.microsoft.com/en-us/library/yd1hzczs%28v=vs.100%29.aspx#Singleline
http://msdn.microsoft.com/en-us/library/az24scfc%28v=VS.100%29.aspx
*)
    let log_config__ = Regex.Replace (log_config_, match_, replace, RegexOptions.Singleline)
    log_config__
//#endregion

(* Helper functions used in testing. *)
//#region
/// <summary>Helper function to compare lists.</summary>
let compare_lists_with_compare (compare : 'a -> 'b -> bool) list1 list2 = (list1, list2) ||> List.forall2 (fun item1 item2 -> compare item1 item2)

/// <summary>Helper function to compare lists.</summary>
let compare_lists list1 list2 = compare_lists_with_compare (fun item1 item2 -> item1 = item2) list1 list2

/// <summary>Helper function to compare arrays.</summary>
let compare_arrays_with_compare (compare : 'a -> 'b -> bool) array1 array2 = (array1, array2) ||> Array.forall2 (fun item1 item2 -> compare item1 item2)

/// <summary>Helper function to compare arrays.</summary>
let compare_arrays array1 array2 = compare_arrays_with_compare (fun item1 item2 -> item1 = item2) array1 array2

(* For each test, we create a folder name that has "deleteme" in it, to store any other folders and files needed for that test. This ensures the folder will be deleted by the CleanUp function. Sometimes we have to create a file that doesn't have "deleteme" in the name, such as project.ini. In that case, putting it inside the *deleteme* folder makes sure it is cleaned up. *)
/// <summary>Returns a folder name that will be automatically deleted when all tests are run, based on name (1). Replaces '.' with "_".</summary>
let getTestFolderName name = sprintf "%s\\deleteme_%s" current_dir (Regex.Replace (name, "\.", "_"))
/// <summary>Returns a folder name that will be automatically deleted when all tests are run, based on name (1) and number (2). Replaces '.' with "_".</summary>
let getTestFolderNameWithNumber name number = sprintf "%s_%d" name number |> getTestFolderName

/// <summary>Returns a text file name that will be automatically deleted when all tests are run, based on name (1). Replaces '.' with "_".</summary>
let getTestTextFileName name = sprintf "deleteme_%s.txt" (Regex.Replace (name, "\.", "_"))
/// <summary>Returns a text file name that will be automatically deleted when all tests are run, based on name (1) and number (2). Replaces '.' with "_".</summary>
let getTestTextFileNameWithNumber name number = sprintf "%s_%d" name number |> getTestTextFileName

/// <summary>Create a test text file using the test name (1) in folder (2) that contains contents (3). Return the test file name.</summary>
let CreateTestTextFileInFolder name folder contents =
    do Directory.CreateDirectory folder |> ignore
    let test_file = sprintf "%s\\%s" folder <| getTestTextFileName name
    do File.WriteAllText (test_file, contents)
    test_file
/// <summary>Create a test text file using the test name (1) that contains contents (2). Return the test file name.</summary>
let CreateTestTextFile name contents =
    let folder = getTestFolderName name
    CreateTestTextFileInFolder name folder contents
/// <summary>Create a test text file using the test name (1) and number (2) that contains contents (3). Return the test file name.</summary>
let CreateTestTextFileWithNumber name number contents =
    CreateTestTextFile (sprintf "%s_%d" name number) contents
/// <summary>Create a test text file using the test name (1) and number (2) in folder (3) that contains contents (4). Return the test file name.</summary>
let CreateTestTextFileInFolderWithNumber name number folder contents =
    CreateTestTextFileInFolder (sprintf "%s_%d" name number) folder contents

/// <summary>Write the string (2) to the file (1). Used for creating test-related files such as configuration files.</summary>
let WriteToFile_ path (text : string) =
(* Make sure we can overwrite the file if necessary. *)
    if File.Exists path then do File.SetAttributes(path, FileAttributes.Normal) else () |> ignore
    do File.WriteAllText (path, text)

/// <summary>Run a sequence of tests (1) that do not log an event and do not raise an exception. These tests typically use Assert.* internally to verify the expected outcome.</summary>
let run_tests_that_do_not_log tests =
    for test in tests do
        let name = sprintf "%s.%s" test.part test.name
        printfn "%s: " name
(* Run the test and assert that it does not throw. *)
        Assert.DoesNotThrow(Assert.ThrowsDelegate (fun () -> test.test name))
(* Clear the string. These tests are not automatically validated by checking the log, but we might want to check the log manually, so we clear it between tests to keep one test from affecting another. *)
        _log_string.Clear () |> ignore

/// <summary>Run a sequence of tests (1) that log an event but do not raise an exception.</summary>
let run_tests_that_log tests =
    for test in tests do
        let name = sprintf "%s.%s" test.part test.name
        printfn "%s: " name
(* Run the test and assert that it does not throw. *)
        Assert.DoesNotThrow(Assert.ThrowsDelegate (fun () -> test.test name))
        printfn "%s\n" <| _log_string.ToString ()
(* We have a reference to the output string that Logger uses to record each event. Make sure it contains the name of the test that was just run, which is identical to the event name.
We've had several bugs here.
1. Originally we used Contains to search for the event name. This concealed a bug in which the event name was written to the log as part.name, even though we were searching for part_name. This appeared to work because many tests also create directories that are named after the test, which then show up in the error message, which gave us a match. However, it caused tests to fail when the test did not create any directories.
2. We then switched to StartsWith. However, that ignored the fact that some actions, such as open_project, might log multiple events (in this case, sort order not found and project folder not found. As a result, we switched back to Contains.
3. Contains causes false positives. For example, if the test is named "ReceiveMessage", and the log contains only "ReceiveMessageError", the test passed. For now, we add a space to the test name, since the {eventname} parameter is followed by a space in every event message in logconfig.xml.
*)
        name |> sprintf "%s " |> (_log_string.ToString().Contains) |> Assert.True
(* Clear the string. *)
        _log_string.Clear () |> ignore

/// <summary>Run a sequence of tests (1) that raise exceptions.</summary>
let run_tests_that_throw (tests : seq<TestThatThrows>) =
    for test in tests do
        let name = sprintf "%s.%s" test.part test.name
        printfn "%s: " name
(* Run the test and assert that it throws an exception of the expected type. *)
        let message = Assert.Throws(test.exception_type, fun () -> test.test name).Message
        printfn "%s: %s\n" (test.exception_type.Name) message

/// <summary>Clean up files and directories, such as configuration files, that were created during testing.</summary>
let CleanUp () =
    for file in (Directory.GetFiles <| Directory.GetCurrentDirectory ()) do
        if file.Contains("deleteme") then
(* Make sure the file isn't read-only. *)
            File.SetAttributes(file, FileAttributes.Normal)
            File.Delete file
    for dir in (Directory.GetDirectories <| Directory.GetCurrentDirectory ()) do
        if dir.Contains("deleteme") then
(* Make sure all files in this folder are not read-only. *)
            for file in dir |> Directory.GetFiles do File.SetAttributes (file, FileAttributes.Normal)
(* In FileTreeViewConfigTest, we found that .NET has no built-in way to copy directories. However, Microsoft.VisualBasic does. Here, I was calling Directory.Delete (dir, true), but it was consistently raising exceptions due to the directory not being empty, even though passing true as the second parameter is supposed to delete the directory anyway. I decided, again, to try the Microsoft.VisualBasic version instead, and indeed it seems to work better. *)
            do (new Microsoft.VisualBasic.Devices.Computer ()).FileSystem.DeleteDirectory (dir, Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents)
//#endregion