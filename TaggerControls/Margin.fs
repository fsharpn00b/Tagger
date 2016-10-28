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

// TimeSpan
open System
// DispatcherTimer
open System.Windows.Threading
// DragDrop, DragEventArgs, Input, Rect
open System.Windows
// Canvas
open System.Windows.Controls
// Brushes, DrawingVisual, VisualCollection
open System.Windows.Media

// MaybeMonad
open Monads

(* The types of highlight. Each has a different color. *)
type HighlightType = DragFrom | DragTo | OpenTag

(* We tried these approaches to drawing highlights:
1. Rectangle. Problem: this has its own events and they interfere with our handling of margin events.
2. RectangleGeometry/GeometryDrawing. Problem: I couldn't figure out how to display them except by using a Path, which has the same problem as #1.
3. DrawingVisual. This is a more lightweight way of drawing anyway. For more information see:
http://msdn.microsoft.com/en-us/library/ms742254.aspx
*)

/// <summary>Highlights of a single type to display on the margin. (1) The margin. (2) The margin collection of highlights, so we can add and remove these highlights to and from the collection. (3) The color for the highlights. (4) The name of this instance for debugging.</summary>
type private Highlight (canvas : Canvas, collection : VisualCollection, brush, name) as this =
(* Member values. *)
//#region
(* We use this to draw the highlights. *)
    let _dv = new DrawingVisual ()
(* The shapes of the highlights. *)
    let mutable _rects = []
(* We use this to make sure we do not erase the highlights when they are already erased. *)
    let mutable _visible = false
(* If this timer is set, it removes the highlights when it ticks. *)
    let _clear_timer = new DispatcherTimer ()
//#endregion

(* Event handlers. *)
//#region
(* We do not mark this event handled. *)
/// <summary>Handler for clear timer Tick event. (1) Event args. Return unit.</summary>
    let tick_handler _ =
        do
(* Hide the highlights. *)
            this.hide ()
(* Stop the timer. *)
            _clear_timer.Stop ()
//#endregion

(* Constructor. *)
//#region
(* Add event handlers. *)
    do _clear_timer.Tick.Add tick_handler
//#endregion

(* Methods. *)
//#region
/// <summary>Show highlights using each pair of Y positions in (1), for (2) milliseconds. If (2) is 0.0, show the highlights until they are erased. Return unit.</summary>
    member this.show (y_positions : (float * float) list) time =
(* If the list of pairs of Y positions is empty, or the highlights are already visible, stop. *)
        if y_positions.Length = 0 || _visible then ()
        else
(* Get the highlight coordinates relative to the margin. X1 is 0.0 and X2 is the width of the margin. *)
            do _rects <- y_positions |> List.map (fun (top, bottom) -> new Rect (0.0, top, canvas.ActualWidth, bottom - top))
(* Get the drawing context. *)
            let dc = _dv.RenderOpen ()
            do
(* Draw the highlights using the brush specified in the constructor. *)
                _rects |> List.iter (fun rect -> dc.DrawRectangle (brush, null, rect))
(* Close the drawing context. *)
                dc.Close ()
(* Add the DrawingVisual to the collection specified in the constructor, in a thread-safe way. For more information see the hide method. *)
                lock collection.SyncRoot (fun () -> collection.Add _dv |> ignore)
                _visible <- true
(* Set the interval for removing the highlights. *)
            if time > 0 then
                do
                    _clear_timer.Interval <- (float) time |> TimeSpan.FromMilliseconds
(* Start the timer. *)
                    _clear_timer.Start ()

/// <summary>Hide the highlights. Return unit.</summary>
    member this.hide () =
(* If the highlights are already removed, stop. *)
        if _visible = false then ()
        else
            do
(* Remove the DrawingVisual from the collection specified in the constructor, in a thread-safe way. *)
(* There's no way to simply erase an object. We tried drawing over the old object with a new object using the same brush as the canvas background, but then the object still exists and might have a higher Z order than other objects we want to draw over it. The only sure way we've found to remove it is to remove the DrawingVisual from the VisualCollection. *)
                lock collection.SyncRoot (fun () -> collection.Remove _dv)
                _visible <- false

/// <summary>Redraw the highlights using each pair of Y positions in (1). Reset the timer. Return unit.</summary>
    member this.redraw y_positions =
(* If the highlights are already removed, stop. *)
        if _visible = false then ()
        else
            do
(* Stop the timer. *)
                _clear_timer.Stop ()
(* Hide the highlights. *)
                this.hide ()
(* Redraw the highlights at the specified Y positions. Reset the timer. *)
(* Note for future debugging of any DispatcherTimer or TimeSpan. Dispatcher.Interval is a TimeSpan. Interval.Milliseconds, which is type int, returns 0, even when the interval is not 0 milliseconds. That might be because the value is converted to larger time units if possible. In our case, the interval was 10000 milliseconds and that might have been converted to 10 seconds. Call Interval.TotalMilliseconds, which is type float, to get the correct value. *)
                this.show y_positions <| (int) _clear_timer.Interval.TotalMilliseconds

/// <summary>Scroll the highlights to match vertical scroll offset change (1). Return unit.</summary>
    member this.scroll vertical_scroll_offset_change =
(* Get the top and bottom of the current rectangle and subtract the vertical scroll offset change. *)
        do _rects |> List.map (fun rect -> rect.Top - vertical_scroll_offset_change, rect.Bottom - vertical_scroll_offset_change) |> this.redraw

/// <summary>Return true if the highlights are visible and any of them contains the Y position (1); otherwise, false.</summary>
    member this.contains_y_position y_position =
        _visible && _rects |> List.exists (fun rect -> y_position >= rect.Top && y_position <= rect.Bottom)

//#endregion

/// <summary>The data we send to drop a line.</summary>
type MarginDropData = {
(* The Y position on which the line was dropped. *)
    y_position : double;
(* True if the source and target are the same TaggerMargin; otherwise, false. *)
    same_margin : bool
(* They keys that were pressed during the drop. *)
    key_states : DragDropKeyStates
}

and TaggerMargin () as this =
    inherit Canvas ()

(* Events. *)
//#region
/// <summary>The user selected a line. (1) The Y position the user clicked. (2) True if the Control key was pressed. (3) True if the Shift key was pressed.</summary>
    let _select = new Event<double * bool * bool> ()
/// <summary>The user right-clicked a line. (1) The Y position the user clicked. (2) True if the Control key was pressed. (3) True if the Shift key was pressed.</summary>
    let _right_click = new Event<double * bool * bool> ()
(* The user drops a line. *)
    let _drop = new Event<MarginDropData> ()
/// <summary>The user starts a drag. (1) The Y position the user started dragging from. (2) True if the Control key was pressed. (3) True if the Shift key was pressed.</summary>
    let _drag_start = new Event<double * bool * bool> ()
/// <summary>The user drags over the margin. (1) The Y position the user is dragging over.</summary>
    let _drag_over = new Event<double> ()
/// <summary>The user drags out of the margin.</summary>
    let _drag_leave = new Event<unit> ()
/// <summary>The user ended a drag.</summary>
    let _end_drag = new Event<unit> ()
(* Used for debugging. PaneController adds the string to the document in its editor. *)
    let _debug = new Event<string> ()
//#endregion

(* Member values. *)
//#region
(* The highlights for this margin. *)
    let _highlights = new VisualCollection (this)
    let _open_tag_rects = new Highlight (this, _highlights, Brushes.Blue, "Open Tag")
    let _drag_from_rects = new Highlight (this, _highlights, Brushes.Red, "Drag From")
    let _drag_to_rects = new Highlight (this, _highlights, Brushes.Green, "Drag To")
//#endregion

(* Highlight helper functions. *)
//#region
/// <summary>Show the open tag highlights using each pair of Y positions in (1), for (2) milliseconds. If (2) is 0.0, show the highlights until they are erased. Return unit.</summary>
    let show_open_tag_rects y_positions time =
        do
(* First hide the existing open tag highlights. *)
            _open_tag_rects.hide ()
            _open_tag_rects.show y_positions time

/// <summary>Show the drag from highlights using each pair of Y positions in (1), for (2) milliseconds. If (2) is 0.0, show the highlights until they are erased. Return unit.</summary>
    let show_drag_from_rects y_positions time =
        do
(* First hide the existing drag from highlights. *)
            _drag_from_rects.hide ()
            _drag_from_rects.show y_positions time

/// <summary>Show the drag to highlights using each pair of Y positions in (1), for (2) milliseconds. If (2) is 0.0, show the highlights until they are erased. Return unit.</summary>
    let show_drag_to_rects y_positions time =
        do
(* Remove the open tag highlights, so they do not interfere with the drag and drop highlights. *)
            _open_tag_rects.hide ()
(* First hide the existing drag to highlights. *)
            _drag_to_rects.hide ()
            _drag_to_rects.show y_positions time
//#endregion

(* Event handler helpers. *)
//#region
/// <summary>Return the appropriate drag/drop effect based on the source (1) and target (2) margins, and key states (3).</summary>
    let get_drag_effect source target (keystates : DragDropKeyStates) =
(* Set the drag effects based on the key state. *)
        if keystates.HasFlag DragDropKeyStates.ControlKey then DragDropEffects.Copy
        else if keystates.HasFlag DragDropKeyStates.AltKey then DragDropEffects.Link
        else if keystates.HasFlag DragDropKeyStates.ShiftKey then DragDropEffects.Move
        else
(* If the source and target are the same, the default behavior is move. If not, it's tag. *)
            if source.Equals target then DragDropEffects.Move
            else DragDropEffects.Link

/// <summary>Helper for the Drop event handler. (1) The source margin. (2) The target margin. (3) The Y position of the drop. (4) The keys that were pressed during the drop. Return true if the drop succeeds; otherwise, false.</summary>
    let drop_handler_helper source target y_position key_states =
(* Determine whether the source and target are the same TaggerMargin. *)
        let same_margin = source.Equals target
(* Proceed only if the user didn't drop a line on itself in the same margin. *)
        if same_margin && _drag_from_rects.contains_y_position y_position then false
        else
(* Fire the event with: *)
            do _drop.Trigger {
(* The Y position that was dropped on. *)
                y_position = y_position;
(* Whether the source and target are the same. *)
                same_margin = same_margin;
(* The keys that were pressed. *)
                key_states = key_states;
            }
            true

/// <summary>Verify the source of an event (1). If we succeed, return a tuple. (R1) True if the Shift key was pressed. (R2) True if the Control key was pressed. Otherwise, return None.</summary>
    let mouse_up_helper (args : Input.MouseEventArgs) =
(* If the source is empty, or not a TaggerMargin, stop. *)
        if args.Source = null || args.Source :? TaggerMargin = false then None
        else
(* See if the Shift key was pressed. It seems key state information isn't included in Input.MouseEventArgs. *)
            let shift = Input.Keyboard.IsKeyDown (Input.Key.LeftShift) || Input.Keyboard.IsKeyDown (Input.Key.RightShift)
(* See if the Control key was pressed. *)
            let ctrl = Input.Keyboard.IsKeyDown (Input.Key.LeftCtrl) || Input.Keyboard.IsKeyDown (Input.Key.RightCtrl)
            Some (shift, ctrl)
//#endregion

(* Event handlers. *)
//#region
(* We do not mark this event handled. *)
/// <summary>Handler for MouseMove event. (1) Event args. Return unit.</summary>
    let mouse_move_handler (args : Input.MouseEventArgs) =
(* Don't proceed until the mouse is moving and the left button is pressed. *)
        if Input.Mouse.PrimaryDevice.LeftButton <> Input.MouseButtonState.Pressed
(* If the source is empty, or not a TaggerMargin, stop. *)
            || args.Source = null
            || args.Source :? TaggerMargin = false then ()
        else
(* See if the Shift key was pressed. It seems key state information isn't included in Input.MouseEventArgs. *)
            let shift = Input.Keyboard.IsKeyDown (Input.Key.LeftShift) || Input.Keyboard.IsKeyDown (Input.Key.RightShift)
(* See if the Control key was pressed. *)
            let ctrl = Input.Keyboard.IsKeyDown (Input.Key.LeftCtrl) || Input.Keyboard.IsKeyDown (Input.Key.RightCtrl)
(* Fire the drag_start event to let the PC know that the selection might need to be changed. *)
            do _drag_start.Trigger ((this |> args.GetPosition).Y, ctrl, shift)
(* Start a drag operation. Record the margin that was dragged. *)
(* DragDropEffects.All does not mean All. *)
            let effects = DragDropEffects.Copy ||| DragDropEffects.Link ||| DragDropEffects.Move ||| DragDropEffects.None
            do DragDrop.DoDragDrop (this, this, effects) |> ignore

(* Note: For some reason, dropping does not trigger mouse left button up. This is what we want, but I'm not sure why it works this way. *)
(* We do not mark this event handled. *)
/// <summary>Handler for MouseLeftButtonUp event. (1) Event args. Return unit.</summary>
    let mouse_left_up_handler (args : Input.MouseEventArgs) =
        match args |> mouse_up_helper with
        | None -> ()
(* Get the Y position the user clicked. Fire the event. *)
        | Some (shift, ctrl) -> do _select.Trigger ((this |> args.GetPosition).Y, ctrl, shift)

(* We do not mark this event handled. *)
/// <summary>Handler for MouseRightButtonUp event. (1) Event args. Return unit.</summary>
    let mouse_right_up_handler (args : Input.MouseEventArgs) =
        match args |> mouse_up_helper with
        | None -> ()
(* Get the Y position the user clicked. Fire the event. *)
        | Some (shift, ctrl) -> do _right_click.Trigger ((this |> args.GetPosition).Y, ctrl, shift)

(* We do not mark this event handled. *)
/// <summary>Handler for Drop event. (1) Event args. Return unit.</summary>
    let drop_handler (args : DragEventArgs) =
(* Hide the drag to highlights. *)
        do _drag_to_rects.hide ()
(* If we successfully validate the source and target... *)
        match FileTreeViewItem.get_source_and_target<TaggerMargin> args with
        | Some (source, target) ->
(* Get the Y position that the user dropped on. *)
            let y_position = (this |> args.GetPosition).Y
(* Call the handler helper. *)
            do drop_handler_helper source target y_position args.KeyStates |> ignore
        | None -> ()

(* We mark this event handled. *)
/// <summary>Handler for DragOver event. (1) Event args. Return unit.</summary>
    let drag_over_handler (args : DragEventArgs) =
(* Mark the event handled, otherwise our changes to DragEventArgs.Effects are ignored. *)
        do args.Handled <- true
(* Strangely, it's possible to call get_source_and_target without a type parameter. In that case, it doesn't work properly, I assume because it assumes the type parameter is obj. I don't know why the compiler doesn't complain - is it possible to partially apply functions with regard to type parameters as well as regular parameters? *)
(* If we fail to validate the source and target, set the drag effect to "no" and stop. *)
        match FileTreeViewItem.get_source_and_target<TaggerMargin> args with
        | None -> do args.Effects <- DragDropEffects.None
        | Some (source, _) -> this.drag_over_helper source args

(* We do not mark this event handled. *)
/// <summary>Handler for DragLeave event. (1) Event args. Return unit.</summary>
    let drag_leave_handler (args : DragEventArgs) =
(* DragEventArgs.Source is actually the target, i.e. the item being dropped on. If the target is empty, or not a TaggerMargin, stop. *)
        if args.Source = null || args.Source :? TaggerMargin = false then ()
(* Notify PaneController, so it can hide the drag to highlights for both margins. *)
        else do _drag_leave.Trigger ()

(* We do not mark this event handled. *)
/// <summary>Handler for QueryContinueDrag event. (1) Event args. Return unit.</summary>
    let query_continue_drag_handler (args : QueryContinueDragEventArgs) =
(* If the user pressed Escape... *)
        if args.EscapePressed then
            do
(* Cancel the drag. *)
                args.Action <- DragAction.Cancel
(* Hide the drag to highlights. *)
                _drag_to_rects.hide ()
(* Fire an event to the PaneController, so it can stop the editor from scrolling due to dragging. *)
                _end_drag.Trigger ()
//#endregion

(* Constructor. *)
//#region
    do
(* Set properties. *)
(* The margin should be disabled until the editor has a file loaded. PaneController detects that and sets this property to true. *)
        this.IsEnabled <- false
(* Give the margin a background that distinguishes it from the editor. *)
        this.Background <- Brushes.DarkGray
(* It seems the default value is false. *)
        this.AllowDrop <- true
(* Add event handlers. *)
        this.MouseMove.Add mouse_move_handler
        this.MouseLeftButtonUp.Add mouse_left_up_handler
        this.MouseRightButtonUp.Add mouse_right_up_handler
        this.Drop.Add drop_handler
        this.DragOver.Add drag_over_handler
        this.DragLeave.Add drag_leave_handler
        this.QueryContinueDrag.Add query_continue_drag_handler
//#endregion

(* Methods. *)
//#region
(* This is needed for when a selection is cleared. *)
/// <summary>Hide the drag from highlights. Return unit.</summary>
    member this.hide_drag_from_rects = _drag_from_rects.hide

(* This is needed for when the user drags from the margin to the editor, then outside the editor. The editor notifies the PaneController, which calls this. *)
/// <summary>Hide the drag to highlights. Return unit.</summary>
    member this.hide_drag_to_rects = _drag_to_rects.hide

/// <summary>Scroll all visible highlights to match vertical scroll offset change (1). Return unit.</summary>
    member this.scroll vertical_scroll_offset_change =
        do
            vertical_scroll_offset_change |> _open_tag_rects.scroll
            vertical_scroll_offset_change |> _drag_from_rects.scroll
            vertical_scroll_offset_change |> _drag_to_rects.scroll

/// <summary>Show highlights of type (1) using each pair of Y positions (2) for (3) milliseconds. Return unit.</summary>
    member this.show highlight_type y_positions time =
        let draw_function =
            match highlight_type with
            | OpenTag -> show_open_tag_rects
            | DragFrom -> show_drag_from_rects
            | DragTo -> show_drag_to_rects
        do draw_function y_positions time

/// <summary>Redraw highlights of type (1) using each pair of Y positions in (2). Return unit.</summary>
    member this.redraw highlight_type y_positions =
        let redraw_function =
            match highlight_type with
            | OpenTag -> _open_tag_rects.redraw
            | DragFrom -> _drag_from_rects.redraw
            | DragTo -> _drag_to_rects.redraw
        do redraw_function y_positions

(* This has to be a method so it can be called by PaneController.editor_margin_drag_handler in response to the user dragging from the margin and over the editor. *)
(* Also see Editor.move_and_copy_helper, which does not allow the target line to be any of the source lines. *)
/// <summary>Helper for the DragOver event handler. Set the appropriate drag/drop effect and fire the drag over event to the PaneController. (1) The margin where the drag started. (2) The drag event args. Return unit.</summary>
    member this.drag_over_helper source (args : DragEventArgs) =
(* Get the Y position that we're over. *)
        let y_position = (this |> args.GetPosition).Y
(* If we're over the same pane and line(s) from which the drag started... *)
        if
            source.Equals this &&
            _drag_from_rects.contains_y_position y_position then
(* Hide the drag to highlights. *)
            do _drag_to_rects.hide ()
(* Set the drag effect to "no". *)
            do args.Effects <- DragDropEffects.None
(* Set the drag effects based on the source and target margins and the key state. *)
        else
            do args.Effects <- get_drag_effect source this args.KeyStates
(* Fire the event. PaneController will determine what line corresponds to our Y position and display the drag to highlights. *)
            do y_position |> _drag_over.Trigger
//#endregion

(* Expose events. *)
    member this.select = _select.Publish
/// <summary>The user right-clicks a line. (1) The Y position the user clicked. (2) True if the Control key was pressed. (3) True if the Shift key was pressed.</summary>
    member this.right_click = _right_click.Publish
    member this.drop = _drop.Publish
    member this.drag_start = _drag_start.Publish
    member this.drag_over = _drag_over.Publish
    member this.drag_leave = _drag_leave.Publish
/// <summary>The user ended a drag.</summary>
    member this.end_drag = _end_drag.Publish
    member this.debug = _debug.Publish

(* We have to implement these to use DrawingVisual. See the MSDN link at the top. *)
    override this.VisualChildrenCount
        with get () = _highlights.Count
    override this.GetVisualChild index =
        if index < 0 || index >= _highlights.Count then raise <| new ArgumentOutOfRangeException ()
        else _highlights.[index]

(* Expose functions for testing. *)

