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

module FileTreeViewHelpers

(* Added reference: System.Runtime.Serialization. *)

// Directory
open System.IO
// DataContract, DataMember
open System.Runtime.Serialization
// Regex
open System.Text.RegularExpressions

// List extensions
open ListHelpers

///<summary>Represents the information content of a FileTreeView without the UI part.</summary>
[<DataContract>]
type FileTreeNode = {
(* http://stackoverflow.com/questions/4034932/f-serialization-of-record-types
The compiler turns the field into a read-only property, so you have to apply the attribute to the field directly. *)
    [<field: DataMember(Name="dir") >]
    dir : string;
    [<field: DataMember(Name="dirs") >]
    dirs : FileTreeNode list;
    [<field: DataMember(Name="files") >]
    files : string list;
    }

(* Search helper functions. *)
//#region
///<summary>Find the first FileTreeNode in (2) whose dir (path) field matches (1). Return a FileTreeNode option if found; otherwise, return None.</summary>
let rec search_ftn name (ftn : FileTreeNode) =
    if ftn.dir = name then Some ftn
    else ftn.dirs |> List.tryPick (search_ftn name)

/// <summary>Recurse through FileTreeNode (1) and filter the files using patterns (2). If (2) is empty, return the FTN unchanged. Otherwise, return the filtered FTN.</summary>
let filter_ftn (file_patterns : string list) ftn =
    let rec filter_ftn_helper ftn = {
        ftn with
(* For each file, see if any of the patterns matches it. If so, get it; otherwise, discard it. *)
            files = ftn.files |> List.filter (fun file ->
(* Note that Regex.IsMatch will return true if the pattern matches any part of the file name. It does not have to match the entire file name unless the pattern includes ^ and $ (the begining and end of string markers). *)
                file_patterns |> List.exists (fun pattern -> Regex.IsMatch (file, pattern))
            )                    
        }
(* If no file patterns were specified, return the FTN unchanged. *)
    if file_patterns.IsEmpty then ftn
    else ftn |> filter_ftn_helper

///<summary>Get all directories and files under the directory (1). Return a FileTreeNode option if the directory exists; otherwise, return None.</summary>
let dir_to_ftn root_dir =
/// <summary>Return a FileTreeNode based on directory (1).</summary>
    let rec get_dir dir = {
        dir = dir
        files = dir |> Directory.GetFiles |> Array.toList
(* Return a FileTreeNode for each subdirectory. *)
        dirs = dir |> Directory.GetDirectories |> Array.toList |> List.map get_dir;
        }
(* Check that the directory exists. *)
    if Directory.Exists root_dir then get_dir root_dir |> Some
    else None
//#endregion

(* Path conversion helper functions. *)
//#region
(* These two functions, for converting FTNs between relative and absolute paths, were intended to allow a sort order FTN to be used to sort a project FTN, even if the directories did not match because the project had been moved since the sort order FTN was created. For example, if a project was created in c:\project and thus the sort order FTN files and folders had paths based on c:\project, we wanted to be able to move the project to c:\work\project and still apply the sort order FTN based on the relative, not absolute, paths. However, we found a simpler solution than using these two functions. We simply trim the file and folder paths of the project and sort order FTNs before comparing them. I've left these functions in, in case we find some other use for them. *)
/// <summary>Convert the absolute file and folder paths in the FileTreeNode (1) and all its children to relative paths.</summary>
let convert_ftn_paths_to_relative (ftn : FileTreeNode) =
(* Get the dir field of the root FTN. We treat this as the root directory for this and all child FTNs. *)
    let root_dir = ftn.dir
(* If this is the root FTN, removing the root directory will leave a dir field value of "". If it is a child FTN, however, the root directory will be followed by a backslash, which we now remove. If the backslash is not present, Trim will have no effect. *)
    let remove_root_dir (path : string) = path.Replace(root_dir, "").Trim('\\')
(* Recursive helper to convert the FTN (1) to relative paths. *)
    let rec convert_ftn (ftn : FileTreeNode) =
(* Return an FTN with the folder and file paths converted. *)
        {
            ftn with
(* Note that we remove only the root directory from the dir field, leaving the relative directory structure intact. For example, root_dir\\a\\b becomes a\\b. However, we use trim to remove the entire path from each item in the files field. For example, root_dir\\a\\a_1 becomes a_1. *)
                dir = ftn.dir |> remove_root_dir
                files = ftn.files |> List.map (fun file -> file |> Path.GetFileName)
(* Recurse into the child FTNs. *)
                dirs = ftn.dirs |> List.map (fun ftn -> ftn |> convert_ftn)
        }
(* Start with the root FTN. *)
    convert_ftn ftn

/// <summary>Convert the relative file and folder paths in the FileTreeNode (2) and all its children to absolute paths, based on root directory (1).</summary>
let convert_ftn_paths_to_absolute root_dir (ftn : FileTreeNode) =
(* Helper function to append the relative folder path of an FTN to the root directory. *)
    let add_dir (path : string) =
(* The root FTN has relative folder path "". Don't append the "" to the root directory because it introduces a trailing backslash. *)
        if path.Length > 0 then sprintf "%s\\%s" root_dir path
        else root_dir
(* Recursive helper to convert the FTN (1) to absolute paths. *)
    let rec convert_ftn (ftn : FileTreeNode) =
(* Return an FTN with the folder and file paths converted. *)
        {
            ftn with
(* Set the absolute directory path for this FTN. *)
                dir = add_dir ftn.dir
(* For the files field, we have only names and no directory structure. So, for each file, build the path from (1) the root directory, (2) the relative path for this FTN, and (3) the file name. *)
                files = ftn.files |> List.map (fun file -> sprintf "%s\\%s" (add_dir ftn.dir) file)
(* Recurse into the child FTNs. *)
                dirs = ftn.dirs |> List.map (fun ftn -> ftn |> convert_ftn)
        }
(* Start with the root FTN. *)
    convert_ftn ftn
//#endregion

(* Sort helper functions. *)
//#region
///<summary>Check to see if the files (2) in the directory (1) have a display order specified for them in a FileTreeNode in (3). Return the file list, sorted or not depending on whether the FileTreeNode was found.</summary>
let sort_files dir files (sort_order_ftn : FileTreeNode) =
(* If the FileTreeNode has a node for the specified directory... *) 
    match search_ftn dir sort_order_ftn with
    | Some node ->
(* Sort the files according to the display order in the FileTreeNode. The compare function trims the paths of the files, so that only their names are compared; this allows us to apply a sort order FTN to a project FTN even if the project has moved and now has a different root directory than when the sort order FTN was created. *)
        List.merge_sort_with_compare node.files files (fun file1 file2 -> Path.GetFileName file1 = Path.GetFileName file2)
(* Simply return all files from the directory. *)
    | None -> files

///<summary>Check every directory in FileTreeNode (1) to see if it has a matching directory in FileTreeNode (2). If so, sort the file list in the directory in FTN (1) to match the sort order of the files in the directory in FTN (2).</summary>
let rec sort_all_files (ftn_to_sort : FileTreeNode) (sort_order_ftn : FileTreeNode) = {
(* Create a new FTN based on FTN (1). *)
    ftn_to_sort with
(* Update the file list for FTN (1) based on the sort order for the file list in the matching directory, if any, in FTN (2). *)
        files = sort_files ftn_to_sort.dir ftn_to_sort.files sort_order_ftn
(* Update the subnodes for FTN (1) by recursing. We do not change FTN (2), as we want all of its subnodes (directories) to be available for a directory match with each directory (subnode) in FTN (1). *)
        dirs = ftn_to_sort.dirs |> List.map (fun dir -> sort_all_files dir sort_order_ftn)
    }

///<summary>Check to see if the subfolders (2) in the directory (1) have a display order specified for them in a FileTreeNode in (3). Return the folder list, sorted or not depending on whether the FileTreeNode was found.</summary>
(* We tried to combine this with sort_files, but it seems that merge_sort_list_with_compare can't be used to return either a string list or FTN list within the same function, even if we specified its return type as generic. Choice<> didn't seem to work either. *)
let sort_folders dir dirs (ftn : FileTreeNode) =
(* If the FileTreeNode has a node for the specified directory... *) 
    match search_ftn dir ftn with
    | Some node ->
(* Sort the subfolders according to the display order in the FileTreeNode. The compare function trims the paths of the directories of the FTNs; for the reason, see sort_files. *)
        List.merge_sort_with_compare node.dirs dirs (fun item1 item2 -> Path.GetFileName item1.dir = Path.GetFileName item2.dir)
(* Simply return all subfolders from the directory. *)
    | None -> dirs

///<summary>Check every directory in FileTreeNode (1) to see if it has a matching directory in FileTreeNode (2). If so, sort the subfolder list in the directory in FTN (1) to match the sort order of the subfolders in the directory in FTN (2). Return the sorted FTN (1).</summary>
let rec sort_all_folders (ftn_to_sort : FileTreeNode) (sort_order_ftn : FileTreeNode) =
(* First sort the subfolders. *)
    let dirs_ = sort_folders ftn_to_sort.dir ftn_to_sort.dirs sort_order_ftn
(* Create a new FTN based on FTN (1). *)
    {
        ftn_to_sort with
(* Recurse into the subfolders. *)
            dirs = dirs_ |> List.map (fun dir -> sort_all_folders dir sort_order_ftn)
    }
//#endregion