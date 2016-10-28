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

module ListHelpers

type List<'a> with
(* This is used to move items to the front of a list. *)
///<summary>Return a list of all items in list (1), followed by all items in list (2). Duplicate items are removed.</summary>
    static member move_to_head list1 list2 = List.append list1 list2 |> Seq.distinct |> Seq.toList

///<summary>Return a list that contains item (1).</summary>
    static member singleton item = [item]

(* This is similar to:
list1 |> List.filter (fun item1 -> list2 |> List.exists (fun item2 -> compare item1 item2))
but more efficient, since matches are removed from list 2. *)
///<summary>Return a list of all items that are in both list (1) and (2), using compare function (3). Lists 1 and 2 can be of different lengths and types, but the items returned are from list 1.</summary>
    static member match_with_compare (list1 : 'a list) (list2 : 'b list) (compare : 'a -> 'b -> bool) =
(* Loop through list1, with a tuple of an empty list and list2 as the accumulator. Each item in list1 that matches an item in list2 is removed from list2 and added to the empty list. *)
        ((list2, []), list1) ||> List.fold (fun (list2_, acc) item1 ->
(* Split list2 into matches and non-matches. *)
            let matches, non_matches = list2_ |> List.partition (fun item2 -> compare item1 item2)
(* If there are any matches, add item1 to the accumulator. *)
            let acc = if matches.Length > 0 then item1::acc else acc
(* The new accumulator is the remaining items in list2, and the matches from list1. When we are done, discard the remaining items from list2. *)
            non_matches, acc) |> snd

///<summary>Get all items that are in both list (1) and (2), in the order they appear in list (1), followed by the items that are only in (2), in the order they appear in list 2. (3) is a compare function for items in lists (1) and (2). Return the combined list.</summary>
    static member merge_sort_with_compare list1 list2 (compare : 'a -> 'b -> bool) =
(* Loop through list1, with a tuple of an empty list and list2 as the accumulator. We will remove matches from list2 and add them to the empty list. *)
        let result = (([], list2), list1) ||> List.fold (fun (result_list, list2_) list1_item ->
(* Split list2 into matches and non-matches. There should only be one match, but it's easier to handle the match as a list. *)
            let matches, non_matches = list2_ |> List.partition (fun list2_item -> compare list1_item list2_item)
(* The new accumulator is the current matches added to the previous matches, and the remaining items from list2 (non-matches). *)
            List.concat [result_list; matches], non_matches
        )
(* Add the remaining items from list2 to the accumulated matches. *)
        List.concat [fst result; snd result]

///<summary>Get all items that are in both list (1) and (2), in the order they appear in list (1), followed by the items that are only in (2), in the order they appear in list 2. Items in lists (1) and (2) are compared using equality. Return the combined list.</summary>
    static member merge_sort list1 list2 = List.merge_sort_with_compare list1 list2 (fun item1 item2 -> item1 = item2)

/// <summary>Apply function (1) to the list parameter (2) using mapi to include a counter. Then apply choose to the result to eliminate all items that are None.</summary>
    static member choosei f = List.mapi f >> List.choose id

/// <summary>Return a list that contains no duplicate entries based on the keys returned by function (1).</summary>
    static member distinctBy f = List.toSeq >> Seq.distinctBy f >> Seq.toList

/// <summary>Return the last item in list (1). Precondition: (1) is not empty.</summary>
    member this.last with get () =
        let rec last_ = function
            | hd :: [] -> hd
            | hd :: tl -> last_ tl
            | _ -> failwith "List.last was called on an empty list."
        this |> last_

/// <summary>Return a shallow copy by value of list (1).</summary>
    member this.copy () = List.foldBack (fun item acc -> item :: acc) this []