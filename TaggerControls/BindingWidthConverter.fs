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

(* Note This is in TaggerControls so I can use it in Tagger/FindInProject.xaml. I've figured out how to refer to the TaggerControls namespace in XAML, but not to the Tagger project or the TagController module, otherwise that's where this would be. One solution is to combine all modules in the Tagger project into a single namespace, but this isn't a compelling enough reason to do that. *)

(* We found this here:
https://stackoverflow.com/questions/9081377/xaml-calculated-value-can-this-be-done
*)
type BindingWidthConverter () =
    interface System.Windows.Data.IValueConverter with
        member this.Convert (value, target_type, parameter, culture) =
(* If the value is a float, downcast it. Subtract the width of the scroll bars. The text still overlaps the scroll bars unless we subtract another 10. The return type is obj, so upcast the result. *)
            match value with
            | :? float as value_ -> value_ - System.Windows.SystemParameters.VerticalScrollBarWidth - 10.0 :> obj
            | _ -> null
(* This is required by the interface, but not used. *)
        member this.ConvertBack (value, target_type, parameter, culture) =
            null
