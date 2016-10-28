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

module Monads

type MaybeMonad() =
(* If the result is Some(x), extract x and continue. Otherwise, stop. *)
    member this.Bind (result, rest) =
        match result with
        | Some(x) -> rest x
        | None -> None
(* If the result is true, continue. Otherwise, stop. *)
    member this.Bind (result, rest) =
        match result with
        | true -> rest()
        | false -> None
(* If the result is non-null, continue. Otherwise, stop. *)
    member this.Bind (result, rest) =
        match result with
        | null -> None
        | x -> rest x
(* If the result is a non-empty list, continue. Otherwise, stop. *)
    member this.Bind (result, rest) =
        match result with
        | [] -> None
        | x -> rest x
    member this.Return x = Some x

type ListMonad() =
(* Map rest over each item in result, then concatenate all the lists into a single list. *)
    member this.Bind(result, rest) = result |> List.map rest |> List.concat
    member this.Return x = [x]