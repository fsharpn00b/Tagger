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

module TabControlTest

// RoutedEventArgs
open System
// MenuItem, TabItem
open System.Windows.Controls

open Xunit

// Tab, TabControl
open TaggerControls
open TestHelpers

(*
Tab:

Functions to test:
N init_tab_context_menu
x drophelper
N mouse_move_handler
N drop_handler (this is the event handler, which we can't test directly because it takes a DropEventArgs, which is sealed)
N right_mouse_up_handler
N left_mouse_down_handler

Methods:
 select

Properties:
 file
 is_selected

Events:
x _tab_closed
 _tab_all_closed
x _drag_drop (in drophelper)
x _tab_selected (in select)

// TODO1 Update function list. We've already done so for Tab.

TabControl:

Functions:
x selection_changed_helper
/ drop_handler (via Tab.drophelper)
N selection_changed_handler
N tab_selected_handler
x close_tab
x select_tab
x get_tab
x add_tab
x rename_tab

Events:
x _tab_selected
x _tab_closed
N _tab_all_closed (we simply re-fire this event from Tab)
 _save_project
*)

(* Notes. *)
//#region
(*
We tried to test the Drop event directly using:
tab0.RaiseEvent <| new Windows.RoutedEventArgs (TabItem.DropEvent, target)
but got the error: Object of type 'System.Windows.RoutedEventArgs_ cannot be converted to type 'System.Windows.DragEventArgs'.
We tried to do it with the notes below, but we can't create DragEventArgs.

In case we need it later, here's how to send a left button up event:
let args = new Windows.Input.MouseButtonEventArgs (Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, Windows.Input.MouseButton.Left)
do
    args.RoutedEvent <- (ItemClass).MouseLeftButtonUpEvent
    (Item).RaiseEvent args
*)
//#endregion

(* Helper functions. *)
//#region
(* This is needed because we can't simply add a tab with a non-existent file name. *)
/// <summary>Helper function that adds a tab to tab control (1) based on name (2) and number (3). Returns the tab and the tab name.</summary>
let get_tab (tabs : TaggerTabControl) name number =
    let path = CreateTestTextFileWithNumber name number ""
    let tab = tabs.add_tab path
    tab.IsSome |> Assert.True
    tab.Value, path
//#endregion

type TabControlTest () =
    interface ITestGroup with

    member this.tests_log with get () = []

    member this.tests_throw with get () = []

(* Tests that don't log. *)
//#region
    member this.tests_no_log with get () = [
(* Tab tests. *)
(* Test drag and drop. *)
    {
        part = "Tab"
        name = "drophelper"
        test = fun name ->
            let tabs = new TaggerTabControl ()
            let tab0, tab0_name = get_tab tabs name 0
(* The source and target are described by the DragEventArgs passed to the Drop handler, not by the this identifier. TestDrop can be called on any tab, and its identity will not be used in determining the source and target tabs.
We would make the Drop handler static, except that it fires the _drag_drop event up to the TabControl, and that event has to belong to an instance. *)
(* Try an illegal drop where source = target. *)
            do tab0.test_drop_helper tab0 tab0 |> Assert.False
(* Do a legal drop. *)
            let tab1, tab1_name = get_tab tabs name 1
(* This lets us make sure the drag_drop event fires. *)
            let drag_drop_fired = ref false
            do
                tab0.drag_drop.Add <| fun _ -> do drag_drop_fired := true
(* Drop tab 1 on tab 0. *)
                tab0.test_drop_helper tab1 tab0 |> Assert.True
(* Verify that the tabs have changed places. *)
                Assert.True ((tabs.Items.[0] :?> TaggerTabItem).file = tab1_name)
                Assert.True ((tabs.Items.[1] :?> TaggerTabItem).file = tab0_name)
(* Verify that the drag_drop event fired. *)
                Assert.True !drag_drop_fired
    };
(* Test the tab_selected event. *)
    {
        part = "Tab"
        name = "tab_selected"
        test = fun name ->
            let tabs = new TaggerTabControl ()
            let tab0, _ = get_tab tabs name 0
(* This lets us make sure the select_tab event fires. *)
            let select_tab_fired = ref false
            do
(* Note that here we're adding the handler to TaggerTabItem.tab_selected, as opposed to TabControl.tab_selected. *)
                tab0.tab_selected.Add <| fun _ -> do select_tab_fired := true
(* Select the tab. *)
                tab0.select ()
(* Verify that the select_tab event fired. *)
                Assert.True !select_tab_fired
    };
(* Test the tab_closed event. *)
    {
        part = "Tab"
        name = "tab_closed"
        test = fun name ->
            let tabs = new TaggerTabControl ()
            let tab0, _ = get_tab tabs name 0
(* This lets us make sure the tab_closed event fires. *)
            let tab_closed_fired = ref false
            do
                tab0.tab_closed.Add <| fun _ -> do tab_closed_fired := true
(* Close the tab. *)
                let menu_item = tab0.ContextMenu.Items.[0] :?> MenuItem
                menu_item.RaiseEvent (new Windows.RoutedEventArgs (MenuItem.ClickEvent))
(* Verify that the close_tab event fired. *)
                Assert.True !tab_closed_fired
    };

(* TabControl tests. *)

(* Test the selection changed event handler helper. *)
    {
        part = "TabControl"
        name = "selection_changed_helper"
        test = fun name ->
            let tabs = new TaggerTabControl ()
(* Add two tabs. *)
            let tab0, _ = get_tab tabs name 0
            let tab1, _ = get_tab tabs name 1
(* Add an event handler to verify that MainController is or is not notified. *)
            let tab_selected_fired = ref false
            do
                tabs.tab_selected.Add (fun _ -> do tab_selected_fired := true)
(* We don't have a scenario where _enable_selection_changed_handler and _revert_selection_change are both true, because if the former is true, the latter is not checked. *)
(* _enable_selection_changed_handler = false, _revert_selection_change = true *)
(* 1. Try to select a tab without enabling the handler, with reversion of the selection, when no tab is selected. *)
                tabs.test_selection_changed_helper [tab0] []
(* Verify the event did not fire. *)
                Assert.False !tab_selected_fired
(* _enable_selection_changed_handler = true, _revert_selection_change = true *)
(* Enable tab selection. *)
                tabs.test_enable_selection_changed_handler <- true
(* 2. Select a tab with the handler enabled. Note that selection_changed_helper doesn't actually select a tab, it just notifies MainController. (Unless it needs to revert a selection.) So we don't check to see that the tab is selected. *)
                tabs.test_selection_changed_helper [tab0] []
(* Verify the event fired. *)
                Assert.True !tab_selected_fired
(* Reset the event. *)
                tab_selected_fired := false
(* _enable_selection_changed_handler = false, _revert_selection_change = true *)
(* 3. Try to select a tab without enabling the handler, with reversion of the selection, when a tab is already selected, to make sure the selection reverts. *)
(* Disable tab selection again. *)
                tabs.test_enable_selection_changed_handler <- false
(* Try to select the second tab. *)
                tabs.test_selection_changed_helper [tab1] [tab0]
(* Verify the event did not fire. *)
                Assert.False !tab_selected_fired
(* Verify the first tab is selected. Since TabControl reverted the selection, it will be. *)
                Assert.True (tab0.IsSelected)
(* _enable_selection_changed_handler = false, _revert_selection_change = false *)
(* 4. Select a tab without enabling the handler, and without reversion of the selection. *)
(* Disable tab selection revert. *)
                tabs.test_revert_selection_change <- false
(* Select the second tab. In this case, use IsSelected because we actually want to select the tab and make sure the selection isn't reverted. We don't want to use the select method because that enables the handler. The call to the helper should look like this.
tabs.test_selection_changed_helper [tab1] [tab0]
*)
                tab1.IsSelected <- true
(* Verify the event did not fire. *)
                Assert.False !tab_selected_fired
(* Verify the selection was not reverted. *)
                Assert.True (tab1.IsSelected)
    }
(* Test closing a tab. *)
    {
        part = "TabControl"
        name = "close_tab"
        test = fun name ->
            let tabs = new TaggerTabControl ()
(* Add two tabs. *)
            let tab0, _ = get_tab tabs name 0
            let tab1, tab1_name = get_tab tabs name 1
            do
(* Verify the second tab, the most recently added, is selected. *)
(* Currently, we don't auto-select a tab upon adding it. *)
//                Assert.True tab1.IsSelected
(* Close the second tab. *)
                tabs.close_tab tab1_name
(* Verify that the tab closed. *)
                Assert.True (tabs.Items.Count = 1)
(* Verify that the first tab is now selected. *)
(* Currently, we don't auto-select a tab upon closing another tab. *)
//                Assert.True tab0.IsSelected
    };
(* Test selecting a tab. *)
    {
        part = "TabControl"
        name = "select_tab"
        test = fun name ->
            let tabs = new TaggerTabControl ()
(* Add a tab. *)
            let tab0, _ = get_tab tabs name 0
(* Try to select a tab that doesn't exist. *)
            do
                tabs.select_tab 1
(* Verify that no tab is selected. *)
                Assert.True (tabs.SelectedIndex = -1)
(* Select the tab. *)
                tabs.select_tab 0
(* Verify the tab is selected. *)
                Assert.True (tab0.IsSelected)
    }

(* Test getting a tab. *)
    {
        part = "TabControl"
        name = "get_tab"
        test = fun name ->
            let tabs = new TaggerTabControl ()
(* Add a tab. *)
            let tab0, tab0_name = get_tab tabs name 0
            do
(* Try to get the tab. *)
                Assert.True (tabs.get_tab tab0_name).IsSome
(* Try to get a tab that doesn't exist. *)
                Assert.True (tabs.get_tab "no_such_tab").IsNone
    };

(* Test adding a tab. *)
    {
        part = "TabControl"
        name = "add_tab"
        test = fun name ->
            let tabs = new TaggerTabControl ()
            let tab0_name = CreateTestTextFile name ""
            do
                (tabs.add_tab tab0_name).IsSome |> Assert.True
(* Try to add a second tab with the same name. *)
                (tabs.add_tab tab0_name).IsSome |> Assert.True
                Assert.True (tabs.Items.Count = 1)
(* Try to add a tab for a file that does not exist. *)
                (tabs.add_tab ":").IsNone |> Assert.True
(* Adding tabs should not change the currently selected tab. In this case, no tab should be selected. *)
                Assert.True (tabs.SelectedIndex = -1)
    };

(* Test renaming a tab. *)
    {
        part = "TabControl"
        name = "rename_tab"
        test = fun name ->
            let tabs = new TaggerTabControl ()
            let tab, old_path = get_tab tabs name 0
            let new_path = getTestTextFileNameWithNumber name 1
            do
                tabs.rename_tab old_path new_path "new name"
                Assert.True (tabs.get_tab old_path).IsNone
                Assert.True (tabs.get_tab new_path).IsSome
                Assert.True (tab.Header.ToString () = "new name")
    };

(* Test the tab_selected event. *)
    {
        part = "TabControl"
        name = "tab_selected"
        test = fun name ->
            let tabs = new TaggerTabControl ()
            let tab0, _ = get_tab tabs name 0
(* This lets us make sure the select_tab event fires. *)
            let select_tab_fired = ref false
            do
(* Note that here we're adding the handler to TabControl.tab_selected, as opposed to TaggerTabItem.tab_selected. *)
                tabs.tab_selected.Add <| fun _ -> do select_tab_fired := true
(* Select the tab. *)
                tab0.select ()
(* Verify that the select_tab event fired. *)
                Assert.True !select_tab_fired
    };
(* Test the tab_closed event. *)
    {
        part = "TabControl"
        name = "tab_closed"
        test = fun name ->
            let tabs = new TaggerTabControl ()
            let tab0, _ = get_tab tabs name 0
(* This lets us make sure the close_tab event fires. *)
            let close_tab_fired = ref false
            do
(* Note that here we're adding the handler to TabControl.tab_closed, as opposed to TaggerTabItem.tab_closed. *)
                tabs.tab_closed.Add <| fun _ -> do close_tab_fired := true
(* Close the tab. *)
                let menu_item = tab0.ContextMenu.Items.[0] :?> MenuItem
                menu_item.RaiseEvent (new Windows.RoutedEventArgs (MenuItem.ClickEvent))
(* Verify that the close_tab event fired. *)
                Assert.True !close_tab_fired
    };
    ]
//#endregion