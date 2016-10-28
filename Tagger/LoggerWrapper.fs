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

module LoggerWrapper

// IDisposable
open System

// Logger
open Logger

(* We need to be able to use the Logger throughout the application, but only instantiate it in the main program or the test program, whose source file is usually at the end of the list and includes all the others. The simplest way to do this is to make the logger a global variable, but null until someone instantiates it. The Logger class doesn't have null as a value, so we add this wrapper to it.

The reason we can't instantiate it here is that we need to provide a log config file, and we prefer to use Directory.GetCurrentDirectory to locate it. However, if this module is used by the test program, the current directory is that of the test program, which means the log config file has to be duplicated there. I prefer to keep the log config file only in the Tagger application, let the Tagger application find it with GetCurrentDirectory, and hard-code the location in the test program. *)
type LoggerWrapper () =
    let mutable (_logger : Logger option) = None
(* Expose the Log method. We don't bother with the overload that takes a Dictionary, since we don't use it. *)
    member this.Log_ (event, parameters : (string * 'a) list) =
        match _logger with
        | Some logger -> do logger.Log_ (event, parameters)
        | None -> ()
(* Expose the Logger as a property so it can be instantiated later. *)
    member this.Logger
        with get () = _logger
        and set logger = do _logger <- logger
(* Expose the Flush method. *)
    member this.Flush_ () =
        match _logger with
        | Some logger -> do logger.Flush_ ()
        | None -> ()
(* Expose the Dispose method. *)
    member this.Dispose () =
        match _logger with
        | Some logger -> do (logger :> IDisposable).Dispose()
        | None -> ()

(* This logger is used throughout the Tagger application, so we make it global. *)
let _logger = new LoggerWrapper ()