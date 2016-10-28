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

module FindInProjectWindow

(* Reference UIAutomationTypes needed to inherit TabItem. *)

// Nullable
open System
// Path
open System.IO
// Style
open System.Windows
// TabItem
open System.Windows.Controls

// XAML type provider
open FSharpx

// FindWordInFileResult, FindWordInProjectResults
open TaggerControls
// MaybeMonad
open Monads

/// <summary>The tab used in the Find in Project dialog. (1) The file name that the tab represents.</summary>
type FindInProjectTab (file) as this =
    inherit TabItem ()

(* Events. *)
/// <summary>The user selected the tab.</summary>
    let _selected = new Event<unit> ()

(* Constructor. *)
    do
        this.Header <- file

/// <summary>Select the tab. Return unit.</summary>
    member this.select () =
        do
            this.IsSelected <- true
            _selected.Trigger ()
/// <summary>The user selected the tab.</summary>
    member this.selected = _selected.Publish

(* Find In Project dialog. *)
//#region
/// <summary>The Find in Project dialog.</summary>
type private FindInProjectXAML = XAML<"FindInProjectWindow.xaml">

(* We can't expose the XAML-based control directly. We don't inherit from UserControl, because we don't need to use this class within another XAML file and therefore we don't need the Content property. For more information see TaggerGrid. *)
/// <summary>The Find in Project dialog.</summary>
type FindInProjectWindow () =
(* Member values. *)
/// <summary>The actual control based on the XAML.</summary>
    let _find_in_project_window = new FindInProjectXAML ()
/// <summary>The current results for the search.</summary>
    let mutable _results = None
/// <summary>The file the user selects.</summary>
    let mutable _file = System.String.Empty
/// <summary>The result of showing the dialog.</summary>
    let mutable _selection = None, None

(* Helper functions: general. *)
//#region
/// <summary>Convert (1) from Nullable<bool> to bool. Return the bool value.</summary>
    let nullable_to_bool (x : Nullable<bool>) = if x.HasValue && x.Value then true else false
//#endregion

(* Event handler helpers. *)

(* We must specify the type of (1) or F# decides it is a FindTagInProjectResults. *)
/// <summary>Convert FindWordInProjectResults (1) to a string. (2) True to include a temporary one-way tag to the file that contains each result. Return the string.</summary>
    let word_results_to_string (results : FindWordInProjectResults) include_one_way_tags =
(* Convert the project results to a string. *)
        (System.String.Empty, results) ||> List.fold (fun acc (file, results) ->
(* Convert the file results to a string. *)
            let results_ = (System.String.Empty, results) ||> List.fold (fun acc result ->
                let tag =
(* Create a temporary one-way tag with the file index and line number for this result. *)
                    if include_one_way_tags then
                        let tag = TagInfo.format_temp_tag result.file_index result.line_number
                        tag.ToString ()
                    else ""
(* Write the line text and the temporary one-way tag. *)
                sprintf "%s%s%s\n" acc result.line_text tag
                )
            sprintf "%s%s\n\n%s\n" acc file results_
            )

/// <summary>Return the contents of the dialog as a string. (1) True to include a temporary one-way tag to the file that contains each result.</summary>
    let to_string include_one_way_tags =
(* If the dialog has been initialized, convert the find results to a string. *)
        match _results with
        | Some results -> word_results_to_string results include_one_way_tags
        | None -> System.String.Empty

(* Event handlers. *)

(* We do not mark this event handled. *)
/// <summary>Event handler for ListViewItem.PreviewMouseLeftButtonDown. (1) The line number this LVI represents. (2) The event args. Return unit.</summary>
    let lvi_mouse_left_down_handler line_number args =
        do
(* Save the line number that this LVI represents. *)
            _selection <- Some (_file, line_number), None
(* TagController has a reference to an instance of this dialog. If we close it, that reference becomes invalid. So we hide it instead. *)
            _find_in_project_window.Root.Hide ()

(* We do not mark this event handled. *)
/// <summary>Event handler for FindInProjectTab.selected. (1) The tab. (2) The file path that the tab represents. (3) The ListViewItems to display. (4) The event args. Return unit. </summary>
    let tab_selected_handler (tab : TabItem) file items args =
        do
(* Save the file that this tab represents, in case the user selects a line number from this file. We don't get the file path from the tab itself because it contains only the name, not the path. *)
            _file <- file
(* Clear the ListView. *)
            _find_in_project_window.Lines.Items.Clear ()
(* Add the items to the ListView. *)
            items |> Seq.iter (fun item -> item |> _find_in_project_window.Lines.Items.Add |> ignore)

(* TagController has a reference to an instance of this dialog. If we close it, that reference becomes invalid. So we hide it instead. See:
http://msdn.microsoft.com/en-us/library/system.windows.window.hide%28v=vs.110%29.aspx
"If you want to show and hide a window multiple times during the lifetime of an application, and you don't want to re-instantiate the window each time you show it, you can handle the Closing event, cancel it, and call the Hide method. Then, you can call Show on the same instance to re-open it." *)
/// <summary>Window closing handler. If the user closes the dialog, hide it instead. (1) The event args. Return unit.</summary>
    let closing_handler (args : System.ComponentModel.CancelEventArgs) =
        do
(* Cancel the closing. *)
            args.Cancel <- true
(* Clear the selection. *)
            _selection <- None, None
(* Hide the dialog. *)
            _find_in_project_window.Root.Hide ()

/// <summary>Handle the Copy button. Return unit.</summary>
    let copy_handler _ =
        match to_string false with
        | result when result.Length > 0 ->
            do
(* Hide the dialog. *)
                _find_in_project_window.Root.Hide ()
(* Save the contents of the dialog to the clipboard as a string. *)
                "Find in Project results copied to clipboard." |> MessageBox.Show |> ignore
                result |> System.Windows.Clipboard.SetText
        | _ -> ()

/// <summary>Handle the Dump button. Return unit.</summary>
    let dump_handler _ =
        match to_string true with
        | result when result.Length > 0 ->
            do
(* Save the contents of the dialog as a string. *)
                _selection <- None, Some result
(* Hide the dialog. *)
                _find_in_project_window.Root.Hide ()
        | _ -> ()

(* Constructor helpers. *)

(*
Resizing the Find In Project window does not resize the TextBlock elements that contain the find results. We tried the following solutions.

1. A TextBlock style.
<Style TargetType="TextBlock" x:Key="TextBlockStyle">
    <Setter Property="Width" Value="{Binding ActualWidth, ElementName=Lines}" />
</Style>
Problem: We can't do a calculation like: Lines.ActualWidth - SystemParameters.VerticalScrollBarWidth
This is the approach we used. We solved the calculation problem with a converter. See BindingWidthConverter.fs.

2. Set property in code.
textblock.Width <- _find_in_project_window.Lines.ActualWidth - SystemParameters.VerticalScrollBarWidth - 10.0
Problem: A change to Lines.ActualWidth doesn't update textblock.Width.

3. A binding.
See:
http://msdn.microsoft.com/en-us/library/ms742863%28v=vs.110%29.aspx
See sections "Remarks" and "Examples" in:
http://msdn.microsoft.com/en-us/library/system.windows.data.binding%28v=vs.110%29.aspx

let binding = new System.Windows.Data.Binding ()
do
    binding.Source <- _find_in_project_window.Lines.ActualWidth - SystemParameters.VerticalScrollBarWidth - 10.0
    textblock.SetBinding (TextBlock.WidthProperty, binding) |> ignore
Problem: This seems to work when the window is first displayed, but the TextBlock width isn't updated when the ListView ActualWidth changes.
Note at first we thought this was because ActualWidth isn't a dependency property. However, based on #5, #6, and #7, we think the problem is with the binding.
// Note We probably needed to set Path.
Note setting binding.Source to object.property does not work. Binding seems to require:
binding.Source <- object
binding.Path <- property
to work correctly.

4. Create a custom dependency property to expose ActualWidth and bind to that.
See:
http://social.msdn.microsoft.com/Forums/vstudio/en-US/91462784-8b9b-4366-830c-30f167a09147/binding-actualheight-and-actualwidth-to-viewmodel?forum=wpf
https://stackoverflow.com/questions/1602148/binding-to-actualwidth-does-not-work

In TaggerControls:
namespace TaggerControls

// Size
open System.Windows
// DockPanel
open System.Windows.Controls

type SizeNotifyPanel () as this =
    inherit DockPanel ()

    static let _size = DependencyProperty.Register("Size", typeof<Size>, typeof<SizeNotifyPanel>, null)
    let size_changed_handler (args : SizeChangedEventArgs) = do this.Size <- args.NewSize
    do this.SizeChanged.Add size_changed_handler
    member this.Size
// Note If we ever use this code, we need to use match instead of downcast.
        with get () = this.GetValue (SizeNotifyPanel.SizeProperty) :?> Size
        and set (value : Size) = do this.SetValue (SizeNotifyPanel.SizeProperty, value)
    static member SizeProperty with get () : DependencyProperty = _size
// Note The exposed property should be made up of ActualHeight/ActualWidth, not args.NewSize.

In FindInProject.xaml:
<tc:SizeNotifyPanel Name="Panel" HorizontalAlignment="Stretch">
    <TabControl Name="Tabs" DockPanel.Dock="Top" />
    <ListView Name="Lines" DockPanel.Dock="Bottom" />
</tc:SizeNotifyPanel>

Binding:
let binding = new System.Windows.Data.Binding ()
do
    binding.Source <- _find_in_project_window.Panel.Size.Width - SystemParameters.VerticalScrollBarWidth - 10.0
    textblock.SetBinding (TextBlock.WidthProperty, binding) |> ignore

Problem: Doesn't work. We're notified of changes to the dependency property when we use a descriptor (see #5), so the problem seems to be the binding.
We tried:
let binding = new System.Windows.Data.Binding ("SizeNotifyPanel.SizeProperty")
but that doesn't work either.
We tried calling UpdateLayout on the panel, the ListView, the ListViewItems, and the TextBlocks (see #10 for the code to iterate through them). None worked.

5. #4, but instead of a binding to the custom dependency property, use a descriptor.
See: Expert F# 3.0 page 473.
let desc = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(SizeNotifyPanel.SizeProperty, typeof<SizeNotifyPanel>)
do desc.AddValueChanged(_find_in_project_window.Panel, fun _ _ ->
    do textblock.Width <- _find_in_project_window.Panel.Size.Width - SystemParameters.VerticalScrollBarWidth - 10.0)
Problem: Works, but we wanted to use a binding.

6. #4, but instead of code binding, use an XAML binding.
<Style TargetType="TextBlock" x:Key="TextBlockStyle">
    <Setter Property="Width" Value="{Binding Size.Width, ElementName=Panel}" />
</Style>
Problem: Works, but same problem as #1.

7. XAML binding to ActualWidth.
<Style TargetType="TextBlock" x:Key="TextBlockStyle">
    <Setter Property="Width" Value="{Binding ActualWidth, ElementName=Lines}" />
</Style>
This works, without the need for a custom dependency property.
Problem: Same problem as #1.

8. Trigger in XAML.
We didn't try this. 
Problem: Can the trigger access a TextBlock style property setter to set the Width property on all TextBlocks? Also, same problem as #1.

9. Update binding manually.
We didn't try this.
See:
https://stackoverflow.com/questions/7403151/binding-issue-actualwidth-on-dynamic-filled-grid
Problem: Every TextBlock has its own binding, so we'd have to store them, then iterate through them and update them, similar to #10.

10. Update width manually.
do _find_in_project_window.Panel.SizeChanged.Add (fun _ ->
    do _find_in_project_window.Lines.Items |> Seq.cast<ListViewItem> |> Seq.iter (fun item ->
// Note If we ever use this code, we need to use match instead of downcast.
            let textblock = item.Content :?> TextBlock
            do textblock.Width <- _find_in_project_window.Panel.Size.Width - SystemParameters.VerticalScrollBarWidth - 10.0
        )
    )
Problem: Works, but ugly.

Another possible option we didn't try: property change notification. See:
http://msdn.microsoft.com/en-us/library/ms743695%28v=vs.110%29.aspx
*)

(* We have to specify the type for file_matches, or F# decides it is a FindTagInFileResult list. *)
/// <summary>Convert FindWordInFileResults to ListViewItems. (1) The file. (2) A FindWordInFileResults. Return the ListViewItems.</summary>
    let matches_to_lvis file (file_matches : FindWordInFileResult list) =
(* If the application resources don't contain the expected text block style, stop. ResourceDictionary apparently doesn't have a TryFind method. *)
        if _find_in_project_window.Root.Resources.Contains "TextBlockStyle" = false then []
        else
            match _find_in_project_window.Root.Resources.["TextBlockStyle"] with
            | :? Style as style ->
(* Loop through the matches. *)
                file_matches |> List.map (fun file_match ->
(* Create an LVI. *)
                    let lvi = new ListViewItem ()
(* Create a TextBlock. We need this for word wrapping. *)
                    let textblock = new TextBlock ()
                    do
(* Apply the TextBlockStyle style in the XAML.
For more information see sections "Relationship of the TargetType Property and the x:Key Attribute" and "Setting Styles Programmatically" in:
http://msdn.microsoft.com/en-us/library/ms745683%28v=vs.110%29.aspx
*)
                        textblock.Style <- style
(* Add the line text to the TextBlock. *)
                        textblock.Text <- file_match.line_text
(* Add the TextBlock to the LVI. *)
                        lvi.Content <- textblock
(* We use Preview because the default handler interferes with ours. We tried using the non-Preview version and marking the event handled, as we do in Editor.mouse_scroll_handler, but that didn't work. *)
(* Pass the line number to the LVI select handler. If the user selects this LVI, we'll know which line number they selected. *)
                        file_match.line_number |> lvi_mouse_left_down_handler |> lvi.PreviewMouseLeftButtonDown.Add
(* Return the LVI. *)
                    lvi
                    )
            | _ -> []

/// <summary>Convert Find in Project results to tabs and ListViewItems. Return unit.</summary>
    let add_file_menu_items (results : FindWordInProjectResults) =
(* Loop through the files that contain matches. *)
        do results |> List.iter (fun (file, matches) ->
(* Create a tab. Use the file name, not the full path, as the tab header. *)
            let tab = new FindInProjectTab (Path.GetFileName file)
(* Convert the matches to ListViewItems. *)
            let items = matches_to_lvis file matches
            do
(* Pass the file to the tab select handler. If the user selects this tab, then selects a line number, we'll know which file they had selected. *)
                tab.selected.Add <| tab_selected_handler tab file items
(* If the user mouses over the tab, select it. *)
                tab.MouseEnter.Add <| fun _ -> tab.select ()
(* Add the tab to the TabControl. *)
                _find_in_project_window.Tabs.Items.Add tab |> ignore
            )
(* Get the first tab. *)
        if _find_in_project_window.Tabs.Items.Count = 0 then ()
        else
            match _find_in_project_window.Tabs.Items.[0] with
            | :? FindInProjectTab as tab ->
(* Select the tab. *)
                do tab.select ()
            | _ -> ()

/// <summary>Clear the dialog. Return unit.</summary>
    let clear () =
        do
(* Clear tabs and ListViewItems. *)
            _find_in_project_window.Tabs.Items.Clear ()
            _find_in_project_window.Lines.Items.Clear ()

/// <summary>Filter the FindWordInProjectResults (1) based on the user's search preferences. Return the filtered results.</summary>
    let filter project_results =
(* Loop through the project results. *)
        project_results |> List.choose (fun (file, file_results) ->
(* Determine whether the user wants to match cases. *)
            let match_case = _find_in_project_window.MatchCase.IsChecked |> nullable_to_bool
(* Determine whether the user wants to match whole words. *)
            let match_whole_word = _find_in_project_window.MatchWholeWord.IsChecked |> nullable_to_bool
(* If the user wants to match cases, filter out the results that do not match. *)
            let results_ = if match_case then file_results |> List.filter (fun result -> result.match_case) else file_results
(* If the user wants to match whole words, filter out the results that do not match. *)
            let results__ = if match_whole_word then results_ |> List.filter (fun result -> result.match_whole_word) else results_
(* If there are any results left, return them. If not, discard the entry for this file from the FindWordInProjectResults. *)
            if results__.Length > 0 then Some (file, results__) else None
        )

/// <summary>Refresh the dialog based on the user's search preferences. Return unit.</summary>
    let refresh () =
(* If the dialog has been initialized... *)
        match _results with
        | Some results ->
(* Filter the FindWordInProjectResults based on the user's search preferences. *)
            let results_ = results |> filter
            do
(* Clear the dialog. *)
                clear ()
(* Add tabs and ListViewItems for the FindWordInProjectResults. *)
                add_file_menu_items results_
        | None -> ()

(* Constructor. *)
    do
(* Add event handlers. *)
        _find_in_project_window.MatchCase.Checked.Add <| fun _ -> refresh ()
        _find_in_project_window.MatchCase.Unchecked.Add <| fun _ -> refresh ()
        _find_in_project_window.MatchWholeWord.Checked.Add <| fun _ -> refresh ()
        _find_in_project_window.MatchWholeWord.Unchecked.Add <| fun _ -> refresh ()
        _find_in_project_window.Copy.Click.Add copy_handler
        _find_in_project_window.Dump.Click.Add dump_handler
        _find_in_project_window.Root.Closing.Add closing_handler

(* Methods. *)

(* We can't expose the XAML-based control outside this module, so we provide aliases to its methods. *)
/// <summary>Show the dialog. Populate the TabControl and ListView with the FindWordInProjectResults (2). (1) True to include only whole word match results. If the user selects a file and line number, return them. If the user clicks Cancel or closes the dialog, return None.</summary>
    member this.ShowDialog match_whole_word results =
        do
(* Clear the selection. *)
            _selection <- None, None
(* Save the results. *)
            _results <- Some results
(* If match_whole_word is true, set the preference to true. If not, do not change it. *)
        if match_whole_word then do _find_in_project_window.MatchWholeWord.IsChecked <- new Nullable<bool> (true)
        do
(* Filter the results based on the selected preferences. *)
            refresh ()
(* Display the dialog. *)
            _find_in_project_window.Root.ShowDialog () |> ignore

(* Properties. *)

/// <summary>Get the result of showing the dialog.</summary>
    member this.selection with get () = _selection

(* Expose the controls for testing. *)
    member this.test_tabs = _find_in_project_window.Tabs
    member this.test_lines = _find_in_project_window.Lines
    member this.test_match_case = _find_in_project_window.MatchCase
    member this.test_match_whole_words = _find_in_project_window.MatchWholeWord
//#endregion