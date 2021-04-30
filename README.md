# Comms-Unity
Easy to use threaded socket communication in unity

## Importing into Project
Go to the Package Manager window, press the add button, and select "Add package from Git URL..."
![image](https://user-images.githubusercontent.com/33668799/116729324-e3b13a80-a99b-11eb-9009-ade4d52a5aee.png)

Paste in this link: `https://github.com/WeibelLab/Comms-Unity.git`
You may get an error saying import failed. **Paste in the link a second time.**
Unity will then import the package and display a series of errors in the Console. Unity will automatically fix all of these, you can safely clear them.

## Using Comms
Add as a component
![image](https://user-images.githubusercontent.com/33668799/116729735-620ddc80-a99c-11eb-8e68-dea699e84394.png)

You'll need a Server and a Client - these can be on the same or different computers. If they are on different computers, make sure unity is allowed through the firewall, and your system recognizes your network as a private network.

1. Setup the server first ![image](https://user-images.githubusercontent.com/33668799/116729923-a9946880-a99c-11eb-8947-7698c05d98c3.png) <br> Choose a large port (1200-49000) <br> I recommend using dynamic messages. You might choose non-dynamic to marginally increase performance or if working with a custom client/server.
2. Setup the client ![image](https://user-images.githubusercontent.com/33668799/116730765-bb2a4000-a99d-11eb-8279-3fe97b964a2d.png) <br> set the `host` to the IP address of your server's computer (`127.0.0.1` if on the same computer) and the `port` to whatever you set your server to listen on.
3. Run both your client and your server and see if you get a connection message ![image](https://user-images.githubusercontent.com/33668799/116731138-2ecc4d00-a99e-11eb-9481-45471c7598ff.png)
4. Now make a script to send messages. Drag in the ReliableCommunication reference onto the component.
```
using Comms;
using UnityEngine;

public class SendMessage : MonoBehaviour
{
    public ReliableCommunication comm;

    private void Start() {
        // Sending Strings
        comm.Send("Hello");

        // Sending JSON
        // JSONObject json = new JSONObject();
        // json.AddField("type", "foo");
        // json.AddField("data", "bar");
        // comm.Send(json);
    }
}
```
5. And a script to receive messages
```
using Comms;
using UnityEngine;

public class ReceiveMessage : MonoBehaviour
{
    public ReliableCommunication comm;

    public void OnMessage(string msg) {
        Debug.Log("Client said: " + msg);
        // Do something
    }
}
```
To hook up the event listener, 
  1. go into the "Data Events" tab
  2. select your message type (`string` for this example) ![image](https://user-images.githubusercontent.com/33668799/116732506-bf575d00-a99f-11eb-8261-c01580692fd1.png)
  3. add an event 
  4. connect to your script ![image](https://user-images.githubusercontent.com/33668799/116732551-d39b5a00-a99f-11eb-8c05-35adf2bc08b4.png)
Now when you run the server then client, your client will send a 'hello' message to the server which will log the message.
6. Add connection/disconnection messages
If you want to know when clients connect or disconnect:
  1. Add code that will run when the events happen (note that the methods receive a `ReliableCommunication` reference):
```
public void OnConnection(ReliableCommunication instance) {
    Debug.Log(instance.name + " connected");
}

public void OnDisconnection(ReliableCommunication instance) {
    Debug.Log(instance.name + " disconnected");
}
```
  2. Go to the "Status Events" tab and link the events to your script ![image](https://user-images.githubusercontent.com/33668799/116733133-9388a700-a9a0-11eb-9b4c-ae507b3656ba.png)
