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

namespace TaggerControls

// GridLength
open System.Windows
// Grid
open System.Windows.Controls

// XAML type provider
open FSharpx

(* Notes. *)
//#region
(*
This is the first control that uses both XAML and code, so I'm putting notes here.
We want a control with the UI defined in XAML and the initialization and event handlers defined in F#. We want the control to be usable, in turn, in either XAML or F#. We ran into issues with the XAML type provider.

If you define the class in XAML, you'll need the XAML<> class from the type provider to get strongly-typed access to the XAML elements in F# (and therefore to add initialization, event handlers, etc to it). However, the XAML<> class can't be inherited or have extensions, which prevents you from doing those directly. (It can't inherit, either, but that wouldn't help anyway.)

So you need to define a wrapper class for the XAML<> class. The wrapper must inherit from UserControl to get the Content property that (I believe) lets it be rendered in XAML. The wrapper exposes the Root property of the XAML<> class through this Content property. It also exposes, as properties, parts of the XAML<> class that it wants to let users access.

http://msdn.microsoft.com/en-us/library/system.windows.controls.usercontrol.aspx
Per this, we get the Content property by inheriting UserControl (or ContentControl or Content).

Another issue: if the XAML<> class is public, VS looks for TaggerGrid.xaml in whatever project consumes the class instead of this one. I tried using an alias for the XAML<> class but that didn't help. So I have to either:
1. Specify the full path to the XAML file.
2. Make sure the XAML<> class is never used outside this module, by marking it private and creating a container class for it and exposing its Root as a property.

To use this class in XAML, it must be in a namespace (not a module) such as: "namespace ABC". The XAML needs a namespace/assembly reference such as xmlns:c="clr-namespace:ABC;assembly=XYZ" and you can use: <c:FileTreeView />.

MainWindow.xaml shows how to use this class in XAML, and how you can set properties either using attributes or sub-elements. I believe you need a sub-element, not an attribute, to instantiate a class, for example: <tc:FileTreeView />.

So, in summary, what we needed to do:
1. Put the F# part of the control in a namespace, not a module.
2. Inherit the wrapper class from UserControl.
3. Set the wrapper class Content property to the XAML<> class Root property.
4. Add the namespace/assembly reference to the consuming XAML.
*)
//#endregion

type private TaggerGridXAML = XAML<"TaggerGrid.xaml">

type TaggerGrid () as this =
(* We inherit this to get the Content property. *)
    inherit UserControl ()

(* Member values. *)
//#region
(* The initial width of the right sidebar. *)
    let mutable _right_sidebar_width = 100.0
(* The maximum fraction of the grid control that the right sidebar can occupy. *)
    let _right_sidebar_max_width_fraction = 0.5
(* The actual control based on the XAML. *)
    let _grid = new TaggerGridXAML ()
//#endregion

(* Constructor. *)
//#region
(* We do not mark this event handled. *)
(* When the right sidebar is collapsed, remember its width. *)
    do _grid.RightSidebar.Collapsed.Add (fun args -> do
        _right_sidebar_width <- _grid.RightSidebarColumn.Width.Value
(* I believe the size is ignored when the type is Auto. There's no constructor that just takes Auto. *)
        _grid.RightSidebarColumn.Width <- new GridLength (0.0, GridUnitType.Auto)
        )
(* We do not mark this event handled. *)
(* When the right sidebar is expanded, restore its previous width. *)
    do _grid.RightSidebar.Expanded.Add (fun args -> do _grid.RightSidebarColumn.Width <- new GridLength (_right_sidebar_width))

(* When the width of the right sidebar changes, make sure it doesn't exceed the max width relative to the grid. *)
    let check_sidebar_width (column : ColumnDefinition) (max_width : float) =
        let max_width_ = _grid.Root.ActualWidth * max_width
        if column.ActualWidth > max_width_ then do column.Width <- new GridLength (max_width_)
(* We do not mark this event handled. *)
(* If the size of the TaggerGrid itself changes, the size of the right sidebar changes as well. *)
    do _grid.Root.SizeChanged.Add (fun args -> do check_sidebar_width (_grid.RightSidebarColumn) _right_sidebar_max_width_fraction)
(* We do not mark this event handled. *)
    do _grid.RightResize.DragCompleted.Add (fun args -> do check_sidebar_width (_grid.RightSidebarColumn) _right_sidebar_max_width_fraction)

(* When the left sidebar is clicked (which might change the content of the control inside), re-set the left sidebar width to Auto. *)
    let auto_size_left_sidebar () = do _grid.LeftSidebarColumn.Width <- new GridLength (0.0, GridUnitType.Auto)
(* We do not mark this event handled. *)
    do _grid.LeftSidebar.Expanded.Add (fun _ -> do auto_size_left_sidebar ())
(* The default handler marks this event as handled, which prevents ours from firing, so we use UIElement.AddHandler. *)
    do _grid.LeftSidebar.AddHandler (UIElement.MouseLeftButtonUpEvent, new RoutedEventHandler (fun _ _ -> do auto_size_left_sidebar ()), true)
(* Expand the left sidebar by default. *)
    do _grid.LeftSidebar.IsExpanded <- true

(* Expose the actual XAML-based control using the Content property inherited from UserControl. *)
    do this.Content <- _grid.Root
//#endregion

(* Methods. *)
//#region
(* The config class uses this property, rather than access the RightSidebar property directly, because we want to check whether the sidebar is expanded or not. That determines whether we get the width from the sidebar control itself or from the private member value that stores the width when the sidebar is collapsed. *)
/// <summary>Get and set the width of the right sidebar, so it can be saved and restored to/from a configuration file.</summary>
    member this.RightSidebarWidth
        with get () =
(* If the sidebar is expanded, get its width. Otherwise, get the value that is used to determine its width when it is expanded. *)
            if _grid.RightSidebar.IsExpanded then _grid.RightSidebarColumn.Width.Value
            else _right_sidebar_width
        and set value =
(* If the sidebar is expanded, set its width. Otherwise, set the value that is used to determine its width when it is expanded. *)
            if _grid.RightSidebar.IsExpanded then do _grid.RightSidebarColumn.Width <- new GridLength (value, GridUnitType.Auto)
            else do _right_sidebar_width <- value
(* Expose the Expand and Collapse methods of the right sidebar so we can access them in response to events from MainController. *)
    member this.ExpandRightSidebar () = if _grid.RightSidebar.IsExpanded = false then do _grid.RightSidebar.IsExpanded <- true
    member this.CollapseRightSidebar () = if _grid.RightSidebar.IsExpanded then do _grid.RightSidebar.IsExpanded <- false
(* Expose the Content properties of the left and right sidebars and the center area so that consumers of this class can insert controls in them. *)
    member this.LeftSidebar
        with get () = _grid.LeftSidebar.Content
        and set control = do _grid.LeftSidebar.Content <- control
    member this.CenterArea
        with get () = _grid.CenterArea.Content
        and set control = do _grid.CenterArea.Content <- control
    member this.RightSidebar
        with get () = _grid.RightSidebar.Content
        and set control = do _grid.RightSidebar.Content <- control
//#endregion