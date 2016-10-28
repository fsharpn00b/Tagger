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

module ListHelpersTest

open Xunit

open ListHelpers
open TestHelpers

type ListHelpersTest () =
    interface ITestGroup with

    member this.tests_log with get () = [
    ]

    member this.tests_throw with get () = [
    ]

    member this.tests_no_log with get () = [
    {
        part = "ListHelpers"
        name = "move_to_head"
        test = fun name ->
            let list1 = ["1"; "2"; "3"]
            do
(* Try to move the top item. The list should be unchanged. *)
                List<_>.move_to_head ["1"] list1 = ["1"; "2"; "3"] |> Assert.True
(* Move an item in the list to the top of the MRU. *)
                List<_>.move_to_head ["3"] list1 = ["3"; "1"; "2"] |> Assert.True
(* Move an item that isn't in the list. *)
                List<_>.move_to_head ["4"] list1 = ["4"; "1"; "2"; "3"] |> Assert.True
    };
    {
        part = "ListHelpers"
        name = "match_with_compare"
        test = fun name ->
            let compare item1 item2 = sprintf "%d" item1 = item2
            let match_lists list1 list2 = List.match_with_compare list1 list2 compare
            match_lists [] [] = [] |> Assert.True
            match_lists [0] [] = [] |> Assert.True
            match_lists [] ["0"] = [] |> Assert.True
            match_lists [0] ["0"] = [0] |> Assert.True
            match_lists [0; 1] ["0"] = [0] |> Assert.True
            match_lists [0] ["0"; "1"] = [0] |> Assert.True
    };
(* We don't have a separate test for merge_sort_list_with_compare, because the merge_sort_list test tests it adequately. *)
(* Test the merge_sort_list function. *)
    {
        part = "ListHelpers"
        name = "merge_sort"
        test = fun name ->
(* The first list is the sort order; the second list is the content. *)
            Assert.True (List<_>.merge_sort [] [] = [])
            Assert.True (List<_>.merge_sort [0] [] = [])
            Assert.True (List<_>.merge_sort [] [0] = [0])
            Assert.True (List<_>.merge_sort [1] [0] = [0])
            Assert.True (List<_>.merge_sort [1] [1] = [1])
            Assert.True (List<_>.merge_sort [1; 2] [2; 1] = [1; 2])
            Assert.True (List<_>.merge_sort [1; 2] [2; 3; 1] = [1; 2; 3])
            Assert.True (List<_>.merge_sort [1; 2; 3] [2; 1] = [1; 2])
            Assert.True (List<_>.merge_sort [1; 2; 3] [2; 4; 1] = [1; 2; 4])
    };
    ]