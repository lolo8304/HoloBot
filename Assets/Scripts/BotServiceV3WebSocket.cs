#if WINDOWS_UWP
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using BestHTTP;
using BestHTTP.WebSocket;
#endif
namespace LoloBotDirectLineV3WebSocket
{

#if WINDOWS_UWP

	// This block of code won't run in Unity's older version of Mono
	// This can only be run in a UWP device like the HoloLens
	public class Conversation
	{
		public string conversationId { get; set; }
		public string token { get; set; }
		public string expires_in { get; set; }
		public string streamUrl { get; set; }
	}

	public class ConversationReference
	{
		public string id { get; set; }
	}

	public class ConversationActitvities
	{
		public Activity[] activities { get; set; }
		public string watermark { get; set; }
		public string eTag { get; set; }
	}

	public class UserId
	{
		public string id { get; set; }
		public string name { get; set; }
	}

	public class ActivityReference
	{
		public string id { get; set; }
	}

	public class ActivityMessage
	{
		public string type { get; set; }
		public UserId from { get; set; }
		public string text { get; set; }
	}

	public class Activity : ActivityMessage
	{
		public string id { get; set; }
		public DateTime timestamp { get; set; }
		public ConversationReference conversation { get; set; }

		public string channelId { get; set; }
		public string replyToId { get; set; }
		public DateTime created { get; set; }
		public Channeldata channelData { get; set; }
		public string[] images { get; set; }
		public Attachment[] attachments { get; set; }
		public string eTag { get; set; }
	}

	public class Channeldata
	{
	}

	public class Attachment
	{
		public string url { get; set; }
		public string contentType { get; set; }
	}

	class KeyRequest
	{
		public string Mainkey { get; set; }
	}

	/*
     * explanations
     *      start conversation
     *          
     * 
     */


	public interface BotCallback
	{
		void OnMessage(string e);
	}


	/// <summary>
	/// The main service used to communicate with a Bot via the Bot Connector and the Direct Line Channel.
	/// This can only run in a UWP client.
	/// </summary>
	public class BotServiceV3WebSocket
	{
		// From the Bot Connector portal, enable the Direct Line channel on your bot
		// Generate and copy your Direct Line secret (aka API key)
		// TO DO: Please use your own key. This one connects to The Maker Show Bot
		private string _APIKEY = "JFMOFhh5p_Q.cwA.Of8.Uqu333UoxpTm_vHND3atQbgNDFMt4lF51Rqdl-1zfpc";
		private string botToken;
		private string activeConversation;
		private string activeWatermark;
		private string newActivityId;
		private string lastResponse;
		private string webSocketURL;
		private WebSocket webSocket;
		private BotCallback callback;

		public BotServiceV3WebSocket()
		{
		}

		public async Task<string> StartConversation()
		{
			using (var client = new HttpClient())
			{
				client.BaseAddress = new Uri("https://directline.botframework.com/");
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				// Authorize
				client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _APIKEY);

				// Get a new token as dummy call
				var keyreq = new KeyRequest() { Mainkey = "" };
				var stringContent = new StringContent(keyreq.ToString());
				HttpResponseMessage response = await client.PostAsync("v3/directline/conversations", stringContent);
				if (response.IsSuccessStatusCode)
				{
					var re = response.Content.ReadAsStringAsync().Result;
					var myConversation = JsonConvert.DeserializeObject<Conversation>(re);
					activeConversation = myConversation.conversationId;
					botToken = myConversation.token;
					webSocketURL = myConversation.streamUrl;
					return myConversation.conversationId;
				}

			}
			return "Error";
		}

		public void StopWebSocket() {
			if (webSocket != null) {
				webSocket.Close (1000, "Bye!");
				webSocket = null;
			}
		}
		public void StartWebSocket(BotCallback callback)
		{
			this.callback = callback;
			Debug.WriteLine("start websocket client on " + this.webSocketURL);
			if (webSocket == null) {
				webSocket = new WebSocket(new Uri(webSocketURL));
				// Subscribe to the WS events
				webSocket.OnOpen += OnOpen;
				webSocket.OnMessage += OnMessageReceived;
				webSocket.OnClosed += OnClosed;
				webSocket.OnError += OnError;
				// Start connecting to the server
				webSocket.Open();
			} else {
				Debug.WriteLine ("WebSocket is aready runnning");
			}
		}

		/// <summary>
		/// Called when the web socket is open, and we are ready to send and receive data
		/// </summary>
		void OnOpen(WebSocket ws)
		{
			Debug.WriteLine(string.Format("-WebSocket Open!\n"));
		}

		/// <summary>
		/// Called when we received a text message from the server
		/// </summary>
		void OnMessageReceived(WebSocket ws, string message)
		{
			Debug.WriteLine(string.Format("-Message received: {0}\n", message));
			var ar = JsonConvert.DeserializeObject<ConversationActitvities>(message);
			var text = GetNewestAcitityFromConversation(ar);
			if (text != null) callback.OnMessage(text);
		}

		/// <summary>
		/// Called when the web socket closed
		/// </summary>
		void OnClosed(WebSocket ws, UInt16 code, string message)
		{
			Debug.WriteLine(string.Format("-WebSocket closed! Code: {0} Message: {1}\n", code, message));
			webSocket = null;
		}

		/// <summary>
		/// Called when an error occured on client side
		/// </summary>
		void OnError(WebSocket ws, Exception ex)
		{
			string errorMsg = string.Empty;
			#if !UNITY_WEBGL || UNITY_EDITOR
			if (ws.InternalRequest.Response != null)
				errorMsg = string.Format("Status Code from Server: {0} and Message: {1}", ws.InternalRequest.Response.StatusCode, ws.InternalRequest.Response.Message);
			#endif

			Debug.WriteLine(string.Format("-An error occured: {0}\n", (ex != null ? ex.Message : "Unknown Error " + errorMsg)));

			webSocket = null;
		}


		public async Task<bool> SendMessage(string message)
		{
			using (var client = new HttpClient())
			{
				string conversationId = activeConversation;

				client.BaseAddress = new Uri("https://directline.botframework.com/");
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				// Authorize
				client.DefaultRequestHeaders.Add("Authorization", "Bearer " + botToken);

				// Send a message
				string messageId = Guid.NewGuid().ToString();
				DateTime timeStamp = DateTime.Now;
				var attachment = new Attachment();
				var myMessage = new ActivityMessage()
				{
					type = "message",
					from = new UserId() { id = "Joe" },
					text = message
				};

				string postBody = JsonConvert.SerializeObject(myMessage);
				String urlString = "v3/directline/conversations/" + conversationId + "/activities";
				HttpContent httpContent = new StringContent(postBody, Encoding.UTF8, "application/json");
				HttpResponseMessage response = await client.PostAsync(urlString, httpContent);
				if (response.IsSuccessStatusCode)
				{
					var re = response.Content.ReadAsStringAsync().Result;
					lastResponse = re;
					var ar = JsonConvert.DeserializeObject<ActivityReference>(re);
					newActivityId = ar.id;
					return true;
				}
				else
				{
					lastResponse = response.Content.ReadAsStringAsync().Result;
				}
				return false;
			}
		}
		public string GetNewestAcitityFromConversation(ConversationActitvities cm)
		{
			if (cm != null)
			{
				for (int i = 0; i < cm.activities.Length; i++)
				{
					var activity = cm.activities[i];
					Debug.WriteLine("activity received = " + activity.text);
					if (!activity.from.id.Equals("Joe") && activity.replyToId != null
						&& activity.text != null && activity.text.Length > 0)
					{
						Debug.WriteLine("activity is response to " + newActivityId);
						return activity.text;
					}
				}
			}
			return null;
		}
	}

#endif

#if !WINDOWS_UWP

/// <summary>
/// This is an empty shim for the BotService within Unity Mono, otherwise we'd get a
/// compilation error in Unity when trying to instantiate this object.
/// </summary>
public class BotServiceV3
{

}
#endif
}

