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

(* References: WindowsBase. *)

(* This describes an F# TCP server. We didn't use it here but it might be useful later.
https://gist.github.com/panesofglass/765088
*)

module AddOnServer

(* DateTime, TimeSpan *)
open System
(* StreamReader, StreamWriter *)
open System.IO
(* IPAddress *)
open System.Net
(* NetworkStream, TcpListener, TcpClient *)
open System.Net.Sockets
(* AutoResetEvent, CancellationTokenSource *)
open System.Threading

(* DispatcherTimer currently does not work here. *)
(* This requires assembly references: WindowsBase. *)
(* DispatcherTimer *)
// open System.Windows.Threading

// LoggerWrapper
open LoggerWrapper

/// <summary>Sends and receives messages to and from the Tagger Firefox add on.</summary>
type AddOnServer () =

(* Events. *)
//#region

/// <summary>The add on server received message (1) from the client.</summary>
    let _message_received = new Event<string> ()

//#endregion

(* Member values. *)
//#region

/// <summary>The host where the server listens.</summary>
    let mutable _host = null
/// <summary>The port where the server listens.</summary>
    let mutable _port = 0
/// <summary>The server.</summary>
    let mutable _server : TcpListener = null
/// <summary>Sends messages to the client.</summary>
    let mutable _writer : StreamWriter = null
/// <summary>True if the server is started; otherwise, false.</summary>
    let _is_started = ref false
/// <summary>True if the server is listening for connections; otherwise, false.</summary>
    let _is_listening = ref false
/// <summary>Locks access to _is_listening.</summary>
    let _is_listening_lock = ref 0
/// <summary>True if the server has a client; otherwise, false.</summary>
    let _is_connected = ref false
/// <summary>Locks access to _is_connected.</summary>
    let _is_connected_lock = ref 0
/// <summary>Cancels the async listening loop.</summary>
    let _token_source = new CancellationTokenSource ()
/// <summary>The interval at which to test the connection.</summary>
    let _connection_test_delay = 5.0 |> TimeSpan.FromSeconds
/// <summary>We use this to wait for any open connection to close, with a timeout.</summary>
    let _connection_close = new AutoResetEvent (false)
/// <summary>The time to wait for the connection to close.</summary>
    let _stop_listening_timeout = 5.0 |> TimeSpan.FromSeconds
/// <summary>The test message to send to determine whether we still have a client connected.</summary>
    let _test_message = "."
//#endregion

(* Functions: general helper. *)
//#region
//#endregion

(* Functions: message sending, receiving, and handling. *)
//#region

/// <summary>Send message (1) to the client. Return unit.</summary>
    let send_message_ (message : string) =
(* We do not lock _is_connected here because it doesn't matter if it changes immediately after this. *)
(* If we are not connected, log an error. *)
        if !_is_connected = false then
            let error = "There is no client connected."
            do _logger.Log_ ("AddOnServer.SendMessageError", ["message", message; "error_message", error])
        else
(* The connection could have failed without our knowing it, so we use exception handling to send the message. *)
            try
                do
(* Send the message to the client. *)
                    message |> _writer.WriteLine
                    _logger.Log_ ("AddOnServer.SendMessage", ["message", message])
            with | ex -> do _logger.Log_ ("AddOnServer.SendMessageError", ["message", message; "error_message", ex.Message])

(* We tried this to test the connection, but for some reason it does not work. *)
// TODO3 It might be a threading issue.
(*
    let keep_alive_timer = new DispatcherTimer ()
    do
        keep_alive_timer.Interval <- TimeSpan.FromSeconds 5.0
        keep_alive_timer.Tick.Add (fun _ ->
            do "Test" |> writer.WriteLine
            if client.Connected = false then do connected := false
            )
        keep_alive_timer.Start ()
*)

/// <summary>Handle message (1). Return unit.</summary>
    let handle_message = _message_received.Trigger

/// <summary>Check for an incoming message using stream (1). If one is available, read it with reader (2). Return the message if one is available; otherwise, return None.</summary>
    let receive_message (stream : NetworkStream) (reader : StreamReader) =
(* This function is called by handle_client, which locks _is_connected. *)
(* If we are not connected, log an error. *)
        if !_is_connected = false then
            let error = "There is no client connected."
            do _logger.Log_ ("AddOnServer.ReceiveMessageError", ["error_message", error])
            None
        else
(* The connection could have failed without our knowing it, so we use exception handling to receive the message. *)
            try
(* Previously, we tried checking StreamReader.EndOfStream = false. However, that blocks for some reason.
Next we tried checking StreamReader.Peek () <> -1. However, that blocks until the first data are received from the client, though it does not block after that. *)
(* See:
http://msdn.microsoft.com/en-us/library/system.net.sockets.networkstream.dataavailable.aspx
*)
(* If the connection is open and there is a message... *)
                if stream.CanRead && stream.DataAvailable then
(* Read the message. *)
                    let message = reader.ReadLine ()
(* Note you should not use ReadLine unless you know the client appends \n to its messages. *)
                    do _logger.Log_ ("AddOnServer.ReceiveMessage", ["message", message])
(* Return the message. *)
                    Some message
                else None
            with | ex ->
                do _logger.Log_ ("AddOnServer.ReceiveMessageError", ["error_message", ex.Message])
                None
//#endregion

(* Functions: connection handling. *)

//#region

/// <summary>Close connection (1). Return unit.</summary>
    let close_connection (client : TcpClient) =
(* We lock _is_connected so that handle_client can't call receive_message or send_test_message while we set _is_connected to false. Those functions log errors if we call them while _is_connected is false. *)
        do
            lock _is_connected_lock (fun () ->
                do
(* Set the _is_connected flag to false. *)
                    _is_connected := false
(* Close the connection. We do this even if the client has already closed it, because the client has a different instance of TcpClient. *)
                    client.Close ()
            )
(* Notify stop_listen that we have closed the connection. *)
            _connection_close.Set () |> ignore
            _logger.Log_ ("AddOnServer.CloseConnection", [])

/// <summary>Send a test message to the client (1) to see if it is still connected. (2) The last time the connection was checked. Return true if the client is still connected; otherwise, return false.</summary>
    let send_test_message (client : TcpClient) (last_time : DateTime ref) =
(* If not enough time has passed since we last tested the connection, stop. *)
        if DateTime.Now.Subtract !last_time < _connection_test_delay then true
(* This function is called by handle_client, which locks _is_connected. *)
(* If we already know we are not connected, log an error. *)
        else if !_is_connected = false then
            let error = "There is no client connected."
            do _logger.Log_ ("AddOnServer.SendTestMessageError", ["error_message", error])
            false
        else
(* Replace (2) with the current time. *)
            do last_time := DateTime.Now
(* See:
http://msdn.microsoft.com/en-us/library/system.net.sockets.tcpclient.connected%28v=vs.110%29.aspx
TcpClient.Connected only returns false after a failed message send. That failure raises an exception that we must handle. *)
(* Try to send the message to the client. *)
            try
                do _test_message |> _writer.WriteLine
(* If an exception was not raised, we are still connected. *)
                true
(* If an exception was raised, we are no longer connected. *)
            with | ex ->
(* Make sure the client does not say it is connected. If it does, there was a different, unexpected error in sending the message, so log it. *)
                if client.Connected then do _logger.Log_ ("AddOnServer.SendTestMessageError", ["error_message", ex.Message])
                false

/// <summary>Set up connection (1). If we succeed, return a tuple. (R1) The client stream. (R2) The client stream reader. If we fail, return None.</summary>
    let set_up_client (client : TcpClient) =
        try
(* This stream lets us send and receive messages. *)
            let stream = client.GetStream ()
(* Use this to receive messages. *)
            let reader = new StreamReader (stream)
            do
(* Use this to send messages. *)
                _writer <- new StreamWriter (stream)
(* Send messages at once. *)
                _writer.AutoFlush <- true
(* Log the event. *)
                _logger.Log_ ("AddOnServer.OpenConnection", [])
(* Return the stream and reader. *)
            Some (stream, reader)
(* If there was an exception while setting up the stream, reader, or writer, log it. *)
        with | ex ->
            do _logger.Log_ ("AddOnServer.OpenConnectionError", ["message", ex.Message])
            None

/// <summary>Handle connection (1). Use stream (2) and reader (3) to receive messages. Return unit.</summary>
    let handle_client client stream reader =
(* Save the current time. We use this to periodically test the connection. *)
        let last_time = ref DateTime.Now
(* We do not lock _is_connected here because it doesn't matter if it changes immediately after this. *)
(* Set the _is_connected flag to true. *)
        _is_connected := true
(* Loop until we are not connected. *)
        while !_is_connected do
(* We lock _is_connected so that handle_listen_loop_cancel can't set it to false while we run the following code. receive_message and send_test_message log an error if we call them while _is_connected is false. *)
            lock _is_connected_lock (fun () ->
(* _is_connected could have been set to false after we checked it and before we locked it, so we check it again here. We can't lock is_connected around the entire loop, because then handle_listen_loop_cancel could never set it to false. *)
                if !_is_connected then
(* Check for an incoming message. If there is one, handle it. *)
                    receive_message stream reader |> Option.iter handle_message
(* Check to see if the client is still connected. *)
                    _is_connected := send_test_message client last_time
            )

(* See:
http://msdn.microsoft.com/en-us/library/ee340460.aspx
http://msdn.microsoft.com/en-us/library/dd997364.aspx
Also consider Async.TryCancelled.
http://msdn.microsoft.com/en-us/library/ee370399.aspx
*)
(* Previously, this code was in a finally block in set_up_client. However, that finally block was not called when the async operation was canceled. *)
(* Note this function is called when the async operation is canceled. It does not function as a finally block for each iteration of listen_loop, which means we also need to close the connection when an iteration of listen_loop completes without being canceled. *)
/// <summary>Handle the closing of the listening loop. Close the connection with client (1). Return unit.</summary>
    let handle_listen_loop_cancel (client : TcpClient) =
(* Close the connection. *)
        do client |> close_connection

/// <summary>Listen for connections and handle them. Loop until canceled.</summary>
    let rec listen_loop () = async {
(* Note this is recursive, and each iteration does not go away until the async operation is cancelled. Then handle_listen_loop_cancel is called for this and every previous iteration.
The problem is that we want to call close_connection if the async operation is cancelled, or if the iteration finishes normally, but not in both cases. TcpClient.Close does not raise an exception if we call it twice, but close_connection logs the event, which is duplicated.
To fix this, we use a reference value to record whether the iteration has finished normally, and close the Async.OnCancel handler over this value. We use a lock to protect the value. *)
        let closed = ref false
        let closed_lock = ref 0
(* Listen for a connection. If another connection comes in while we are handling this one, it will be queued until this one closes. *)
        let! client = _server.AcceptTcpClientAsync () |> Async.AwaitTask
(* Note let! and use! each happen on different threads. I believe we found this with a test that connected to the server and then stopped the server, which caused a conflict. *)
(* Note if the async operation is canceled while we listen for a connection, use! is not called. This means we do not have to worry about passing a null client to handle_listen_loop_cancel. *)
(* Handle the async operation being canceled. The operation is an infinite loop so it must be canceled at some point. *)
        use! async_handler = Async.OnCancel (fun () ->
(* We lock this so it does not interrupt if the iteration finishes normally. *)
            do lock closed_lock (fun () -> if !closed = false then do handle_listen_loop_cancel client))
(* Get the stream and reader for the connection.  *)
        match client |> set_up_client with
(* If we succeeded, handle the connection. *)
        | Some (stream, reader) -> do handle_client client stream reader
(* set_up_client logs an error if it fails. *)
        | None -> ()
(* We lock this so it can't be interrupted if the async operation is canceled. *)
        do lock closed_lock (fun () ->
            do
(* Close the connection. *)
                client |> close_connection
(* The connection has been closed, so it does not need to be closed when the async operation is canceled. *)
                closed := true
        )
(* Continue the loop. *)
        return! listen_loop ()
    }

/// <summary>Listen for incoming connections. Return unit.</summary>
    let start_listen () =
(* If the server is not started, log an error. *)
        if !_is_started = false then
            let error = "The server is not started."
            do _logger.Log_ ("AddOnServer.StartListenError", ["message", error])
        else
(* The server could have stopped without our knowing it, so we use exception handling to start listening. *)
            try
(* We lock _is_listening so stop_listen cannot interrupt us. That was a problem in testing when we started the server and then immediately stopped it. *)
                do lock _is_listening_lock (fun () ->
                    do
(* Start the listen loop on another thread. *)
                        Async.Start (listen_loop (), _token_source.Token)
(* Set the _is_listening flag to true. *)
                        _is_listening := true
                        _logger.Log_ ("AddOnServer.StartListen", [])
                )
            with | ex -> do _logger.Log_ ("AddOnServer.StartListenError", ["message", ex.Message])

/// <summary>Stop listening for incoming connections. Return unit.</summary>
    let stop_listen () =
(* If the server is not started, log an error. *)
        if !_is_started = false then
            let error = "The server is not started."
            do _logger.Log_ ("AddOnServer.StopListenError", ["message", error])
        else
(* We lock _is_listening so we do not interrupt start_listen. *)
        do lock _is_listening_lock (fun () ->
(* If the server is not listening, log an error. *)
            if !_is_listening = false then
                let error = "The server is not listening for incoming connections."
                do _logger.Log_ ("AddOnServer.StopListenError", ["message", error])
            else
                do
(* Set the _is_listening flag to false. *)
                    _is_listening := false
(* Cancel the async operation. This does not raise an exception if the async operation has not started. We were going to test this, but we do not run this unless _is_listening is true, which means the async operation has started (see start_listen). *)
                    _token_source.Cancel ()
        )
(* We do not lock _is_connected here because it doesn't matter if it changes immediately after this. *)
(* If a client is not connected, we do not need to wait for any connection to close. *)
        if !_is_connected = false then do _logger.Log_ ("AddOnServer.StopListen", [])
        else
(* WaitOne returns true if the event was set; false if the timeout elapsed. *)
(* If a client is connected, wait for the connection to close. If we time out, log an error. _token_source.Cancel calls handle_listen_loop_cancel, which sets _connection_close. *)
            if _connection_close.WaitOne _stop_listening_timeout = false then
                do _logger.Log_ ("AddOnServer.StopListenError", [])
            else do _logger.Log_ ("AddOnServer.StopListen", [])

//#endregion

(* Methods: starting and stopping the server. *)
//#region

/// <summary>Start the server and start listening for incoming connections. Return unit.</summary>
    member this.start () =
(* If the server is already started, do nothing. *)
        if !_is_started then ()
        else do
(* See:
http://msdn.microsoft.com/en-us/library/vstudio/system.net.sockets.tcplistener
*)
            _server <- new TcpListener (_host, _port)
            _server.Start ()
(* Set the _is_started flag to true. *)
            _is_started := true
            _logger.Log_ ("AddOnServer.Start", [])
(* Start listening for incoming connections. *)
            start_listen ()

/// <summary>Stop the server, stop listening for incoming connections, and close any open connection. Return unit.</summary>
    member this.stop () =
(* If the server is not started, do nothing. *)
        if !_is_started = false then ()
        else do
(* Stop listening for incoming connections. Close any open connection. *)
            stop_listen ()
            _server.Stop ()
(* Set the _is_started flag to false. *)
            _is_started := false
            _logger.Log_ ("AddOnServer.Stop", [])

(* If we are not connected, this logs an error event, which is fine. *)
/// <summary>Send message (1) to the client. Return unit.</summary>
    member this.send_message = send_message_

/// <summary>Stop the server, stop listening for incoming connections, and close any open connection. Update the host and port to (1) and (2). Start the server and start listening for incoming connections. Return unit.</summary>
    member this.update_host_port host port =
        do
            this.stop ()
            _host <- host
            _port <- port
            this.start ()
//#endregion

(* Events: testing. *)
//#region

/// <summary>The add on server received message (1) from the client.</summary>
    member this.message_received = _message_received.Publish

//#endregion

(* Methods: testing. *)
//#region

    member this.test_handle_message = handle_message
    member this.test_receive_message = receive_message
    member this.test_close_connection = close_connection
    member this.test_send_test_message = send_test_message
    member this.test_set_up_client = set_up_client
    member this.test_handle_client = handle_client
    member this.test_handle_listen_loop_cancel = handle_listen_loop_cancel
    member this.test_listen_loop = listen_loop
    member this.test_start_listen = start_listen
    member this.test_stop_listen = stop_listen

//#endregion

(* Properties. *)
//#region

/// <summary>Get the address where the server listens for incoming connections.</summary>
    member this.host with get () = _host
/// <summary>Get the port where the server listens for incoming connections.</summary>
    member this.port with get () = _port
/// <summary>Get the interval at which the server tests an open connection.</summary>
    member this.connection_test_delay with get () = _connection_test_delay
/// <summary>The test message to send to determine whether we still have a client connected.</summary>
    member this.test_message = _test_message
//#endregion

(* Module values. *)
//#region

(* This server is used throughout the Tagger application, so we make it global. *)
let _server = new AddOnServer ()
(* When Tagger starts, TaggerConfig.load_config calls AddOnServer.update_host_port, which starts the server. *)

//#endregion