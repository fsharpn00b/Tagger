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

(* 
References: PresentationCore, PresentationFramework, System.Xaml, UIAutomationTypes, WindowsBase.
User references: Logger.
*)

(* Note we need to build AvalonEdit before we build this. To do that, load the SharpDevelop solution and build src/Libraries/NRefactory and src/Libraries/ICSharpCode.AvalonEdit. *)

(*
XAML notes:
Always rebuild solution when you change XAML.
When including a user control, even if the namespace is defined in the same assembly, the compiler claimed that it couldn't find the user control type until we specified the assembly.

WPF templates:
http://freshbrewedcode.com/danmohl/2012/03/26/a-nice-addition-to-the-empty-wpf-f-template/
http://visualstudiogallery.msdn.microsoft.com/06c6ece1-2084-4083-a0f7-934fce9d22fb
http://visualstudiogallery.msdn.microsoft.com/e0907c99-bb04-4eb8-9692-9333d5ff4399
User controls in F# (also see downloaded source code):
https://code.google.com/p/fsharpmvcseries/wiki/Composition
*)

// STAThread
open System
// ConfigurationManager
open System.Configuration
// Directory
open System.IO
// Application
open System.Windows

// XAML type provider
open FSharpx

// LoggerWrapper
open LoggerWrapper
// MainController
open MainController
// AddOnServer
open AddOnServer
// TaggerConfig
open Config
// Logger
open Logger

type MainWindow = XAML<"MainWindow.xaml">

(* Instantiate the logger and give it the config file. *)
(* Note the following code requires logconfig.xml to be present in the output directory. We handle this as follows.
1. Add logconfig.xml to the project.
2. In the Properties window for logconfig.xml, set Copy to Output Directory to Copy Always. *)
do _logger.Logger <- new Logger (sprintf "%s\\logconfig.xml" <| Directory.GetCurrentDirectory (), LogConfigFormat.File) |> Some

/// <summary>Save all application information. Return true if we succeed.</summary>
let shutdown (main : MainController) (config : TaggerConfig) =
(* Save the currently open file and project. If we fail, return false. *)
    if main.shutdown () = false then false
    else
        do
(* Save the application configuration. *)
            config.save_config ()
(* Stop the add on server. *)
            _server.stop ()
        true

/// <summary>A global reference to shutdown that the exception handler in start can call.</summary>
let mutable _shutdown = None

(* We do not mark this event handled. *)
/// <summary>Handle the window closing. (1) MainController. (2) Configuration. (3) Event args. Return unit.</summary>
let window_closing_handler main config (args : ComponentModel.CancelEventArgs) =
(* Save all application information. If we fail, cancel the window closing. *)
    if shutdown main config = false then do args.Cancel <- true

(* See:
http://msdn.microsoft.com/en-us/library/system.windows.application.sessionending.aspx
http://msdn.microsoft.com/en-us/library/system.windows.window.closing.aspx
"If a session ends because a user logs off or shuts down, Closing is not raised; handle SessionEnding to implement code that cancels application closure."
*)
(* We do not mark this event handled. *)
/// <summary>Handle the session ending (for example, the user logs out). (1) MainController. (2) Configuration. (3) Event args. Return unit.</summary>
let session_ending_handler main config (args : SessionEndingCancelEventArgs) =
(* Save all application information. If we fail, cancel the session ending. *)
    if shutdown main config = false then do args.Cancel <- true

/// <summary>Load the main window. (1) The application. Return the main window.</summary>
let load_window (app : Application) =
(* Create the main window based on the XAML. *)
    let win = MainWindow ()
(* Create the application configuration. *)
    let config = new TaggerConfig (win.Grid, win.FileTreeView, win.LeftEditor, win.RightEditor, _server)
(* Create a MainController to coordinate events between the controls. *)
// TODO1 What we need is to build a map of status bar children and pass that in as a single param.
    let main = new MainController (config, win.FileTreeView, win.LeftEditor, win.RightEditor, win.LeftPaneLeftMargin, win.LeftPaneRightMargin, win.RightPaneLeftMargin, win.RightPaneRightMargin, win.LeftTabControl, win.RightTabControl, win.LeftStatusChange, win.RightStatusChange)
    do
(* Set the global reference to shutdown and close it over the MainController and TaggerConfig. *)
        _shutdown <- Some <| fun () -> shutdown main config
(* Add event handlers. *)
        win.Root.Closing.Add <| window_closing_handler main config
        app.SessionEnding.Add <| session_ending_handler main config
(* Note: Program is not supposed to be an event coordinator. If any other events arise that need to be coordinated with the grid, then pass the grid to MainController. *)
        main.expand_right_sidebar.Add (fun () -> win.Grid.ExpandRightSidebar ())
(* Load the application configuration. *)
        config.load_config ()
(* Return the window root to be run by the application. *)
    win.Root

(* The following windows handle the Closing event and cancel it.
AddOnCommandWindow
FindInProjectWindow
AddTagWindow
TagCompletionWindow

This is so we only have to create these windows once. However, it also prevents the application from closing them as it shuts down. As a result, when we debug the application and then close the main window, the debugger does not stop until we stop debugging manually. To fix this, we change Application.ShutdownMode from its default value OnLastWindowClose to OnMainWindowClose.

The following code seems able to close all windows even with Application.ShutdownMode set to OnLastWindowClose. We do not know why.
(*
[<STAThread>]
do load_window ()
|> (new Application ()).Run
|> ignore
*)
*)
/// <summary>Start the application. Return unit.</summary>
let start () =
    try
        try
            let app = new Application ()
            let win = load_window app
            do
                app.ShutdownMode <- ShutdownMode.OnMainWindowClose
                win |> app.Run |> ignore
        with | ex ->
(* If the global reference to shutdown is set, call it. We cannot resume the application at this point. *)
            let result =
                match _shutdown with
                | Some shutdown -> shutdown ()
                | None -> false
            let result_ = (string) result
            do
(* Log the exception. *)
                _logger.Log_ ("Program.UnhandledExceptionAlert", ["message", ex.Message; "shutdown", result_])
                _logger.Log_ ("Program.UnhandledExceptionFile", ["message", ex.Message; "shutdown", result_; "stack_trace", ex.StackTrace])
(* We do not call shutdown in the finally block because we might already have called it in the exception handler. So we call shutdown in window_closing_handler and session_ending_handler instead. *)
(* Save the log. Previously, we did this in shutdown, but we want to log the return value from shutdown. *)
    finally do _logger.Dispose ()

// TODO1 If there is an unhandled exception, we would like to resume the application, but since we have already exited Application.Run, we do not know how. We need to add error handlers to every user-accessible event handler.
(* TODO2 Work items.
- Show tabs in title bar like Sumatra
- Use backgroundcolorizer to highlight all other lines with same tags as current line
- Also highlight current line even if not selected.
- Red for selection, blue for tags, green for current line?
*)

[<STAThread>]
start ()

(* Note this is how to delete a revision from SVN. See our Windows build log files for exact folders.
See:
http://blog.onnorokomsoftware.com/blog/2013/11/24/how-to-completely-remove-revision-from-svn-server-visualsvn/
http://serverfault.com/questions/148455/how-to-delete-previous-revisions-with-svn
1. Rename your solution folder.
2. Open a command prompt in the SVN bin folder (e.g. C:\Program Files\TortoiseSVN\bin\).
3. svnadmin dump <repository path>\<repository name> -r<start revision>:<end revision> > <dump file>
End revision is inclusive. Leave out the revisions you want to delete.
4. In VisualSVN Server Manager, delete the repository.
5. Re-create the repository.
6. svnadmin load <repository path>\<repository name> < <dump file>
7. Make sure <repository path>\<repository name>\db\current contains 0.
8. Restart the VisualSVN Server, or the following steps fail with random errors.
9. The local repository (the .svn folder) in your solution folder (for the solution whose revisions you deleted) is now out of sync with the main repository. I don't know how to resync it.
10. In Visual Studio, select VisualSVN > Get Solution from Subversion. Select the repository URL for your solution, and the original name and location (which should be empty, since you renamed the original solution folder).
11. Copy the files from the original solution folder to the newly created solution folder to get any changes that were not checked in.
12. Check in the latest changes.
*)

(* Note this is how to set the application icon.
See:
http://cs.hubfs.net/topic/None/58999
http://www.iconsplace.com/maroon-icons/leaf-3-icon
1. Open a command prompt in C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\Tools\.
2. Run vsvars32.bat.
3. Navigate to the project folder.
4. Create a file named <name>.rc with the following contents.
1 ICON "<name>.ico"
5. Run rc <name>.rc. It creates a file named <name>.res.
6. In project properties > Build > General > Other flags, add --win32res:<name>.res
*)