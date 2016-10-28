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

module Seq

(* Extending Seq is different than extending List. See:
https://stackoverflow.com/questions/671420/static-extension-methods-on-seq-module
*)

/// <summary>Apply function (1) to the list parameter (2) using mapi to include a counter. Then apply choose to the result to eliminate all items that are None.</summary>
let choosei f = Seq.mapi f >> Seq.choose id

/// <summary>Apply function (1) to the list parameter (2) using mapi to include a counter. Then apply tryPick to the result to get the first item that is not None, if any.</summary>
let trypicki f = Seq.mapi f >> Seq.tryPick id