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

module AddOnCommandWindows

// XAML type provider
open FSharpx

/// <summary>The browser tab in which to open a URL.</summary>
type OpenUrlTab = First = 0 | Last = 1 | Next = 2

/// <summary>The Open URL dialog.</summary>
type private OpenUrlWindowXAML = XAML<"OpenUrlWindow.xaml">

(* We can't expose the XAML-based control directly. We don't inherit from UserControl, because we don't need to use this class within another XAML file and therefore we don't need the Content property. For more information see TaggerGrid. *)
/// <summary>The Add Tag dialog.</summary>
type OpenUrlWindow () =

(* Member values. *)

/// <summary>The actual control based on the XAML.</summary>
    let _window = new OpenUrlWindowXAML ()
/// <summary>True if the dialog was canceled; otherwise, false.</summary>
    let mutable _canceled = false

(* Event handler helpers. *)

/// <summary>Cancel the dialog. Return unit.</summary>
    let cancel_helper () =
        do
(* Set the canceled flag. *)
            _canceled <- true
(* TagController has a reference to an instance of this dialog. If we close it, that reference becomes invalid. So we hide it instead. *)
            _window.Root.Hide ()

(* Event handlers. *)

/// <summary>OK button event handler. Close the window. Return unit.</summary>
    let ok_handler _ = do _window.Root.Hide ()

(* TagController has a reference to an instance of this dialog. If we close it, that reference becomes invalid. So we hide it instead. See:
http://msdn.microsoft.com/en-us/library/system.windows.window.hide%28v=vs.110%29.aspx
"If you want to show and hide a window multiple times during the lifetime of an application, and you don't want to re-instantiate the window each time you show it, you can handle the Closing event, cancel it, and call the Hide method. Then, you can call Show on the same instance to re-open it." *)
/// <summary>Window closing handler. If the user closes the dialog, hide it instead. (1) The event args. Return unit.</summary>
    let closing_handler (args : System.ComponentModel.CancelEventArgs) =
        do
(* Cancel the closing. *)
            args.Cancel <- true
(* Proceed as if the user clicked the Cancel button. *)
            cancel_helper ()

(* Method helpers. *)
/// <summary>Initialize the dialog. Return unit.</summary>
    let initialize () =
(* Clear the cancel flag. *)
        _canceled <- false

(* Constructor. *)
    do
        _window.Cancel.Click.Add <| fun _ -> do cancel_helper ()
        _window.OK.Click.Add ok_handler
        _window.Root.Closing.Add closing_handler

(* Methods. *)
/// <summary>Show the dialog. If the user clicks OK, return true. If the user clicks Cancel or closes the dialog, return false.</summary>
    member this.ShowDialog () =
        do
(* Initialize the dialog. *)
            initialize ()
(* Display the dialog. *)
            _window.Root.ShowDialog () |> ignore
(* If the user clicks OK, return true; otherwise, return false. *)
        if _canceled then false else true

(* Properties. *)

/// <summary>Get the browser tab in which to open a URL.</summary>
    member this.OpenUrlTab with get () =
        if _window.First.IsChecked.HasValue && _window.First.IsChecked.Value then OpenUrlTab.First
        else if _window.Last.IsChecked.HasValue && _window.Last.IsChecked.Value then OpenUrlTab.Last
        else OpenUrlTab.Next
/// <summary>Get the value that indicates whether to find a URL in an existing tab.</summary>
    member this.SwitchToExistingTab with get () = _window.SwitchToExistingTab.IsChecked
/// <summary>Get the value that indicates whether to open a URL in a new tab.</summary>
    member this.SwitchToNewTab with get () = _window.SwitchToNewTab.IsChecked
/// <summary>Get the value that indicates whether to open a URL in stand by mode.</summary>
    member this.OpenInStandBy with get () = _window.OpenInStandBy.IsChecked