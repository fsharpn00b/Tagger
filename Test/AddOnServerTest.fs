module AddOnServerTest

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

(* DateTime, TimeSpan *)
open System
(* StreamReader, StreamWriter *)
open System.IO
(* IPAddress *)
open System.Net
(* NetworkStream, TcpListener, TcpClient *)
open System.Net.Sockets
(* Thread *)
open System.Threading

open ICSharpCode.AvalonEdit
open Xunit

// AddOnServer
open AddOnServer
open TestHelpers

(*
Log events:
x OpenConnectionError
x StartListenError
x SendMessageError
x SendTestMessageError
x ReceiveMessageError
N StopListenError (we do not know how to cause this error)
x Start
x Stop
x StartListen
x StopListen
x OpenConnection
x CloseConnection
x SendMessage
x ReceiveMessage

Functions:
x send_message_ (in send_message)
x handle_message
/ receive_message (indirectly, in send_message)
/ close_connection (indirectly, in CloseConnection)
/ send_test_message (indirectly, in send_message)
/ set_up_client (indirectly, in send_message)
/ handle_client (indirectly, in send_message)
/ handle_listen_loop_cancel (indirectly, in send_message)
/ listen_loop (indirectly, in send_message)
/ start_listen (indirectly, in send_message)
/ stop_listen (indirectly, in StopListen)

Methods:
x start (in update_host_port)
x stop (in send_message)
x send_message
x update_host_port (in send_message)

Properties:
N host
N port
N connection_test_delay

Events:
None
*)

(* We tried to test individual functions, but this was complicated by the following.
1. Many of the functions are meant to be called on a separate thread.
2. Once the server starts, it uses the specified host and port, and it calls many of the functions to handle requests, and it consumes messages. If we call the functions manually, we compete with the server, which makes it hard to get consistent results.
*)

let host = "127.0.0.1"
let host_address = IPAddress.Parse host
let port = 13000

/// <summary>Start and stop the server. Return unit.</summary>
let start_and_stop () =
    let server = new AddOnServer ()
(* This calls AddOnServer.start.*)
    server.update_host_port host_address port
(* AddOnServer.listen runs on a separate thread, so give it time to start. *)
    Thread.Sleep 1000
(* Stop the server so we do not block other servers from using the same port. *)
    server.stop ()

(* This function is to test log events. It does not verify the contents of messages. *)
/// <summary>Send a message to the server, and have it send a message. Return unit.</summary>
let send_and_receive () =
    let server = new AddOnServer ()
    let client = new TcpClient ()
    let message = "test"
    do
(* This calls AddOnServer.start. *)
        server.update_host_port host_address port
        client.Connect (host, port)
(* AddOnServer.listen_loop runs on a separate thread, so give it time to open the connection. *)
        Thread.Sleep 1000
(* 1. Send a message from the client to the server. *)
    let writer = new StreamWriter (client.GetStream ())
    do
        writer.AutoFlush <- true
        writer.WriteLine message
(* 2. Send a message from the server to the client. *)
        server.send_message message
(* 3. We don't send a test message from the server to the client because that does not log an event unless there is an error. *)
(* Stop the server so we do not block other servers from using the same port. *)
        server.stop ()

(* This function is to test that messages are received correctly by the client and server. *)
/// <summary>Send (1) messages to the server, and have it send (2) messages to the client. Return unit.</summary>
let send_and_receive_2 client_to_server_message_count server_to_client_message_count =
    let server = new AddOnServer ()
    let client = new TcpClient ()
    let get_client_to_server_message n = n |> sprintf "client_to_server %d"
    let get_server_to_client_message n = n |> sprintf "server_to_client %d"
    do
(* This calls AddOnServer.start. *)
        server.update_host_port host_address port
        client.Connect (host, port)
(* AddOnServer.listen_loop runs on a separate thread, so give it time to open the connection. *)
        Thread.Sleep 1000
    let client_stream = client.GetStream ()
(* 1. Send messages from the client to the server. *)
    let client_writer = new StreamWriter (client_stream)
    do client_writer.AutoFlush <- true
    for loop in 1 .. client_to_server_message_count do
        let message = loop |> get_client_to_server_message
        do
            message |> client_writer.WriteLine
(* Wait for the server to receive the message. *)
            Thread.Sleep 1000
(* Verify the log contains the message. *)
            (_log_string.ToString ()).Contains message |> Assert.True
(* 2. Send messages from the server to the client. *)
    let client_reader = new StreamReader (client_stream)
    for loop in 1 .. server_to_client_message_count do
        let message = loop |> get_server_to_client_message
        do
(* AddOnServer.send_message adds a newline to the message if needed. *)
            message |> server.send_message
(* Wait for the client to receive the message. *)
            Thread.Sleep 1000
(* Read the message. *)
        let mutable server_to_client_message = client_reader.ReadLine ()
(* If the server sent a test message, discard it and read the message again. *)
        if server_to_client_message = server.test_message then do server_to_client_message <- client_reader.ReadLine ()
(* Verify the message. *)
        do server_to_client_message = message |> Assert.True
(* 3. Send a test message from the server to the client. *)
(* Wait for the server to test the connection. *)
    do Thread.Sleep (server.connection_test_delay.Add <| TimeSpan.FromSeconds 1.0)
(* Wait for the client to receive the message. *)
    while (client_stream.DataAvailable = false) do ()
    do
(* Read the message and verify it. *)
        client_reader.ReadLine () = server.test_message |> Assert.True
(* Stop the server so we do not block other servers from using the same port. *)
        server.stop ()

type AddOnServerTest () =
    interface ITestGroup with

    member this.tests_log with get () = [
    {
        part = "AddOnServer"
        name = "OpenConnectionError"
        test = fun name ->
            let server = new AddOnServer ()
(* The client is not connected, so it should raise an exception when we call GetStream on it. *)
            let client = new TcpClient ()
            do server.test_set_up_client client |> ignore
    };
(* Try to listen for incoming connections without starting the server. *)
    {
        part = "AddOnServer"
        name = "StartListenError"
        test = fun name ->
            let server = new AddOnServer ()
            do server.test_start_listen ()
    };
(* Try to send a message while not connected. *)
    {
        part = "AddOnServer"
        name = "SendMessageError"
        test = fun name ->
            let server = new AddOnServer ()
            do server.send_message "test"
    };
(* Try to send a test message while not connected. *)
    {
        part = "AddOnServer"
        name = "SendTestMessageError"
        test = fun name ->
            let server = new AddOnServer ()
            do server.test_send_test_message (new TcpClient ()) (ref <| DateTime.Now.Subtract server.connection_test_delay) |> ignore
    };
(* Try to receive message while not connected. *)
    {
        part = "AddOnServer"
        name = "ReceiveMessageError"
        test = fun name ->
            let server = new AddOnServer ()
            let client = new TcpClient ()
(* AddOnListener.receive_message requires a NetworkStream. To get one from TcpClient.GetStream, we must connect the client. We create another server for it to use. *)
            let tcp_server = new TcpListener (host_address, port)
            do
                tcp_server.Start ()
                tcp_server.AcceptTcpClientAsync () |> ignore
                client.Connect (host, port)
            let stream = client.GetStream ()
            do
                server.test_receive_message stream (new StreamReader (stream)) |> ignore
(* Stop the server so we do not block other servers from using the same port. *)
                tcp_server.Stop ()
    };
(* Currently, we do not know how to cause this error. *)
(*
    {
        part = "AddOnServer"
        name = "StopListenError"
        test = fun name ->
            ()
    };
*)
    {
        part = "AddOnServer"
        name = "Start"
        test = fun name -> do start_and_stop ()
    };
    {
        part = "AddOnServer"
        name = "Stop"
        test = fun name -> do start_and_stop ()
    };
    {
        part = "AddOnServer"
        name = "StartListen"
        test = fun name -> do start_and_stop ()
    };
    {
        part = "AddOnServer"
        name = "StopListen"
        test = fun name -> do start_and_stop ()
    };
    {
        part = "AddOnServer"
        name = "OpenConnection"
        test = fun name ->
            let server = new AddOnServer ()
            let client = new TcpClient ()
            do
(* This calls AddOnServer.start. *)
                server.update_host_port host_address port
                client.Connect (host, port)
(* AddOnServer.listen_loop runs on a separate thread, so give it time to open the connection. *)
                Thread.Sleep 1000
(* Stop the server so we do not block other servers from using the same port. *)
                server.stop ()
    };
    {
        part = "AddOnServer"
        name = "CloseConnection"
        test = fun name ->
            let server = new AddOnServer ()
            let client = new TcpClient ()
            do
(* This calls AddOnServer.start. *)
                server.update_host_port host_address port
                client.Connect (host, port)
(* AddOnServer.listen_loop runs on a separate thread, so give it time to open the connection. *)
                Thread.Sleep 1000
(* Close the connection. *)
                client.Close ()
(* Wait for the server to detect the closed connection. *)
(* For some reason, it takes at least 10 seconds for the server to log the connection close. *)
                Thread.Sleep (server.connection_test_delay.Add <| TimeSpan.FromSeconds 10.0)
(* Verify the server closed the connection in response to the client closing the connection. The server would also have closed the connection when it stopped. *)
                name |> (_log_string.ToString ()).Contains |> Assert.True
(* Stop the server so we do not block other servers from using the same port. *)
                server.stop ()
    };
    {
        part = "AddOnServer"
        name = "SendMessage"
        test = fun name -> do send_and_receive ()
    };
    {
        part = "AddOnServer"
        name = "ReceiveMessage"
        test = fun name -> do send_and_receive ()
    };
    ]

    member this.tests_throw with get () = [
    ]

    member this.tests_no_log with get () = [
    {
        part = "AddOnServer"
        name = "handle_message"
        test = fun name ->
            let server = new AddOnServer ()
            let result = ref ""
            do
                server.message_received.Add <| fun message -> result := message
                server.test_handle_message "test"
                !result = "test" |> Assert.True
    };
    {
        part = "AddOnServer"
        name = "send_message"
        test = fun name -> send_and_receive_2 1 1
    };
    {
        part = "AddOnServer"
        name = "multiple_connections"
        test = fun name ->
            let server = new AddOnServer ()
(* This calls AddOnServer.start. *)
            do server.update_host_port host_address port
            for _ in 1 .. 5 do
(* It seems we cannot re-use the client after we close it. *)
                let client = new TcpClient ()
(* Connect to the server. *)
                client.Connect (host, port)
(* AddOnServer.listen_loop runs on a separate thread, so give it time to open the connection. *)
                Thread.Sleep 1000
(* Disconnect. *)
                client.Close ()
(* Wait for the server to detect the closed connection. *)
                Thread.Sleep (server.connection_test_delay.Add <| TimeSpan.FromSeconds 1.0)
            do
(* Stop the server so we do not block other servers from using the same port. *)
                server.stop ()
(* Write out the log. *)
                _log_string.ToString () |> printfn "%s"
    };
    {
        part = "AddOnServer"
        name = "multiple_messages"
        test = fun name -> send_and_receive_2 5 5
    };
    ]