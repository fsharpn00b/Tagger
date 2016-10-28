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

module GeneralHelpers

// Point, PresentationSource
open System.Windows

(* See:
https://stackoverflow.com/questions/15178682/c-sharp-wpf-window-this-top-returning-wrong-position-on-win8
*)
/// <summary>Convert the point (1) for the DPI setting of the display. Return the converted point.</summary>
let point_to_dpi_safe_point (point : Point) =
    match PresentationSource.FromVisual(Application.Current.MainWindow) with
    | source when source <> null ->
        let dpi_x = 96.0 * source.CompositionTarget.TransformToDevice.M11
        let dpi_y = 96.0 * source.CompositionTarget.TransformToDevice.M22
        new Point (point.X * 96.0 / dpi_x, point.Y * 96.0 / dpi_y)
    | _ -> point

