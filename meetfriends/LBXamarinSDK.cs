/** Generated at 05/22/2016 11:15:01 */

/**
 *** Hardcoded Models ***
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp.Portable;
using LBXamarinSDK;
using LBXamarinSDK.LBRepo;
using System.Net.Http;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Converters;
using RestSharp.Portable.Deserializers;
using System.Diagnostics;
using System.Net.Sockets;
using RestSharp.Portable.HttpClient;

namespace LBXamarinSDK
{
	// Gateway: Communication with Server API
	public class Gateway
    {
		private static Uri BASE_URL = new Uri("http://0.0.0.0:3000/api/");
		private static RestClient _client = new RestClient {BaseUrl = BASE_URL};
        private static string _accessToken = null;
		private static bool _debugMode = false;
        private static CancellationTokenSource _cts = new CancellationTokenSource();
		private static int _timeout = 6000;
		private static bool initFlag = false;

		// Custom deserializer to handle timezones formats sent from loopback
		private class CustomConverter : IDeserializer
        {
            private static readonly JsonSerializerSettings SerializerSettings;
            static CustomConverter ()
            {
                SerializerSettings = new JsonSerializerSettings
                {
                    DateTimeZoneHandling = DateTimeZoneHandling.Local,
                    Converters = new List<JsonConverter> { new IsoDateTimeConverter() },
                    NullValueHandling = NullValueHandling.Ignore
                };
            }

			public T Deserialize<T>(IRestResponse response)
            {
                var type = typeof(T);
                var rawBytes = response.RawBytes;
                return (T)JsonConvert.DeserializeObject (UTF8Encoding.UTF8.GetString (rawBytes, 0, rawBytes.Length), type, SerializerSettings);
            }

            public System.Net.Http.Headers.MediaTypeHeaderValue ContentType { get; set; }
        }

		// Allow Console WriteLines to debug communication with server
		public static void SetDebugMode(bool isDebugMode)
		{
			_debugMode = isDebugMode;
			if(_debugMode)
			{
				Debug.WriteLine("******************************");
				Debug.WriteLine("** SDK Gateway Debug Mode.  **");
				Debug.WriteLine("******************************\n");
			}
		}

		// Sets the server URL to the local address
		public static void SetServerBaseURLToSelf()
        {
            var firstOrDefault = Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (firstOrDefault != null)
            {
                string adrStr = "http://" + firstOrDefault.ToString() + ":3000/api/";
                if (_debugMode)
                    Debug.WriteLine("-------- >> DEBUG: Setting Gateway URL to " + adrStr);
                SetServerBaseURL(new Uri(adrStr));
            }
            else
            {
                if (_debugMode)
                    Debug.WriteLine("-------- >> DEBUG: Error finding self URL.");
                throw new Exception();
            }
        }

		// Debug mode getter
		public static bool GetDebugMode()
		{
			return _debugMode;
		}
		
		/*** Cancellation-Token methods, define a timeout for a server request ***/
		private static void ResetCancellationToken()
		{
			_cts = new CancellationTokenSource();
            _cts.CancelAfter(_timeout);
		}

        public static void SetTimeout(int timeoutMilliseconds = 6000)
        {
			_timeout = timeoutMilliseconds;
			ResetCancellationToken();
        }
		/* *** */

		// Define server Base Url for API requests. Example: "http://10.0.0.1:3000/api/"
        public static void SetServerBaseURL(Uri baseUrl)
        {
            BASE_URL = baseUrl;
            _client.BaseUrl = baseUrl;
        }

		// Sets an access token to be added as an authorization in all future server requests
        public static void SetAccessToken(AccessToken accessToken)
        {
            if (accessToken != null)
                _accessToken = accessToken.id;
        }

		// Get the access token ID currently being used by the gateway
		public static string GetAccessTokenId()
        {
            return _accessToken;
        }

		// Performs a request to determine if connected to server
        public static async Task<bool> isConnected(int timeoutMilliseconds = 6000)
		{
			SetTimeout(timeoutMilliseconds);
			_cts.Token.ThrowIfCancellationRequested();
			try
			{
				var request = new RestRequest ("/", Method.GET);
				var response = await _client.Execute<JObject>(request, _cts.Token).ConfigureAwait(false);
				if (response != null)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			catch(Exception e)
			{
				if (_debugMode)
                    Debug.WriteLine("-------- >> DEBUG: Error: " + e.Message + " >>");	 
				return false;
			}
		}

		// Resets the authorization token
        public static void ResetAccessToken()
        {
            _accessToken = null;
        }
        
		// Makes a request through restSharp to server
		public static async Task<T> MakeRequest<T>(RestRequest request)
        {
            ResetCancellationToken();
            _cts.Token.ThrowIfCancellationRequested();
            _client.IgnoreResponseStatusCode = true;

            if (!initFlag)
            {
                _client.ReplaceHandler(typeof(JsonDeserializer), new CustomConverter());
                initFlag = true;
            }

            var response = await _client.Execute<JRaw>(request, _cts.Token).ConfigureAwait(false);
            var responseData = response.Data != null ? response.Data.ToString() : "";
            
            if (!response.IsSuccess)
            {
			
                if(_debugMode)
                    Debug.WriteLine("-------- >> DEBUG: Error performing request, status code " + (int)response.StatusCode + ", Payload: " + responseData);
                throw new RestException(responseData, (int)response.StatusCode);
            }

            return JsonConvert.DeserializeObject<T>(responseData);
        }


		// Parses a server request then makes it through MakeRequest
        public static async Task<T> PerformRequest<T>(string APIUrl, string json, string method = "POST", IDictionary<string, string> queryStrings = null)
		{
			RestRequest request = null;
			request = new RestRequest(APIUrl, Method.GET);

            if(_debugMode)
                Debug.WriteLine("-------- >> DEBUG: Performing " + method + " request at URL: '" + _client.BuildUri(request) + "', Json: " + (string.IsNullOrEmpty(json) ? "EMPTY" : json));

			// Add query parameters to the request
            if (queryStrings != null)
            {
                foreach (var query in queryStrings)
                {
                    if (!string.IsNullOrEmpty(query.Value))
                    {
                        request.AddParameter(query.Key, query.Value, ParameterType.QueryString);
                    }
                }
            }

			// Add authorization token to the request
            if (!String.IsNullOrEmpty(_accessToken))
            {
                request.AddHeader("Authorization", _accessToken);
            }

			// Add body parameters to the request
			if ((method == "POST" || method == "PUT") && json != "")
            {
				request.AddHeader("ContentType", "application/json");
				request.AddParameter ("application/json", JObject.Parse(json), ParameterType.RequestBody);
			}

			// Make the request, return response
			var response = await MakeRequest<T>(request).ConfigureAwait(false);
			return response;
		}

        // T is the expected return type, U is the input type. E.g. U is Car, T is Car
        public static async Task<T> PerformPostRequest<U, T>(U objToPost, string APIUrl, IDictionary<string, string> queryStrings = null)
        {
            var res = await PerformRequest<T>(APIUrl, JsonConvert.SerializeObject(objToPost), "POST", queryStrings).ConfigureAwait(false);
            return res;
        }

        // T is the expected return type. For example "Car" for get or "Car[]" for get all cars
        public static async Task<T> PerformGetRequest<T>(string APIUrl, IDictionary<string, string> queryStrings = null)
        {	
            var res = await PerformRequest<T>(APIUrl, "", "GET", queryStrings).ConfigureAwait(false);
            return res;
        }

        // T is the expected return type, U is the input type. E.g. U is Car, T is Car
        public static async Task<T> PerformPutRequest<U, T>(U objToPut, string APIUrl, IDictionary<string, string> queryStrings = null)
        {
            var res = await PerformRequest<T>(APIUrl, JsonConvert.SerializeObject(objToPut), "PUT", queryStrings).ConfigureAwait(false);
            return res;
        }
    }

	// Base model for all LBXamarinSDK Models
	public abstract class LBModel
    {
        public virtual String getID()
        {
            return "";
        }
    }

	// Allow conversion between the return type of login methods into AccessToken, e.g. "AccessToken myAccessToken = await Users.login(someCredentials);
	// TODO: Add this jobject->class implicit conversion as a templated function for all classes inheriting from model
	public partial class AccessToken : LBModel
    {
        public static implicit operator AccessToken(JObject jObj)
        {
            if (jObj == null)
            {
                return null;
            }
            return JsonConvert.DeserializeObject<AccessToken>(jObj.ToString());
        }
    }

	// Access Token model
	public partial class AccessToken : LBModel
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string id { get; set; }

        [JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]
        public long? _ttl { get; set; }
		[JsonIgnore]
		public long ttl
		{
			get { return _ttl ?? new long(); }
			set { _ttl = value; }
		}

        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? _created { get; set; }
		[JsonIgnore]
		public DateTime created
		{
			get { return _created ?? new DateTime(); }
			set { _created = value; }
		}


        [JsonProperty("userID", NullValueHandling = NullValueHandling.Ignore)]
        public string userID { get; set; }

		public override String getID()
        {
            return id;
        }
    }
	// GeoPoint primitive loopback type
	public class GeoPoint : LBModel
	{
		// Must be leq than 90: TODO: Add attributes or setter limitations
		[JsonProperty("lat", NullValueHandling = NullValueHandling.Ignore)]
		public double Latitude { get; set; }

		[JsonProperty("lng", NullValueHandling = NullValueHandling.Ignore)]
		public double Longitude { get; set; }
	}

	// Exception class, thrown on bad REST requests
	class RestException : Exception
    {
		public int StatusCode { get; private set; }

		private static int parseStatusCode(string responseString)
		{
            Regex statusCodeRegex = new Regex(@"[0-9]{3}");
            if (statusCodeRegex.IsMatch(responseString))
            {
                Match match = statusCodeRegex.Match(responseString);
				return Int32.Parse(match.Groups[0].Value);
			}
			else
			{
				return 0;
			}
		}

		public RestException(string responseString) : base(responseString)
		{
			StatusCode = parseStatusCode(responseString);
		}

		public RestException(string responseString, int StatusCode) : base(responseString)
		{
            this.StatusCode = StatusCode;
		}
    }
}
/**
 *** Dynamic Repositories ***
 */

namespace LBXamarinSDK
{
    namespace LBRepo
    {
		/* CRUD Interface holds the basic CRUD operations for all models.
		   In turn, all repositories will inherit from this.
		*/
        public abstract class CRUDInterface<T> where T : LBModel
        {
			private static readonly Dictionary<string, string> APIDictionary = new Dictionary<string, string>
            {
				{"event/create", "events"}, 
				{"event/upsert", "events"}, 
				{"event/exists", "events/:id/exists"}, 
				{"event/findbyid", "events/:id"}, 
				{"event/find", "events"}, 
				{"event/findone", "events/findOne"}, 
				{"event/updateall", "events/update"}, 
				{"event/deletebyid", "events/:id"}, 
				{"event/count", "events/count"}, 
				{"event/prototype$updateattributes", "events/:id"}, 
				{"user/create", "users"}, 
				{"user/upsert", "users"}, 
				{"user/exists", "users/:id/exists"}, 
				{"user/findbyid", "users/:id"}, 
				{"user/find", "users"}, 
				{"user/findone", "users/findOne"}, 
				{"user/updateall", "users/update"}, 
				{"user/deletebyid", "users/:id"}, 
				{"user/count", "users/count"}, 
				{"user/prototype$updateattributes", "users/:id"}, 
				{"eventparticipant/create", "participants"}, 
				{"eventparticipant/upsert", "participants"}, 
				{"eventparticipant/exists", "participants/:id/exists"}, 
				{"eventparticipant/findbyid", "participants/:id"}, 
				{"eventparticipant/find", "participants"}, 
				{"eventparticipant/findone", "participants/findOne"}, 
				{"eventparticipant/updateall", "participants/update"}, 
				{"eventparticipant/deletebyid", "participants/:id"}, 
				{"eventparticipant/count", "participants/count"}, 
				{"eventparticipant/prototype$updateattributes", "participants/:id"}, 
				{"friend/create", "friends"}, 
				{"friend/upsert", "friends"}, 
				{"friend/exists", "friends/:id/exists"}, 
				{"friend/findbyid", "friends/:id"}, 
				{"friend/find", "friends"}, 
				{"friend/findone", "friends/findOne"}, 
				{"friend/updateall", "friends/update"}, 
				{"friend/deletebyid", "friends/:id"}, 
				{"friend/count", "friends/count"}, 
				{"friend/prototype$updateattributes", "friends/:id"}, 
				{"friendinvitation/create", "friendInvitations"}, 
				{"friendinvitation/upsert", "friendInvitations"}, 
				{"friendinvitation/exists", "friendInvitations/:id/exists"}, 
				{"friendinvitation/findbyid", "friendInvitations/:id"}, 
				{"friendinvitation/find", "friendInvitations"}, 
				{"friendinvitation/findone", "friendInvitations/findOne"}, 
				{"friendinvitation/updateall", "friendInvitations/update"}, 
				{"friendinvitation/deletebyid", "friendInvitations/:id"}, 
				{"friendinvitation/count", "friendInvitations/count"}, 
				{"friendinvitation/prototype$updateattributes", "friendInvitations/:id"}, 
			};

			// Getter for API paths of CRUD methods
			protected static String getAPIPath(String crudMethodName)
            {
				Type baseType = typeof(T);
				String dictionaryKey = string.Format("{0}/{1}", baseType.Name, crudMethodName).ToLower();

				if(!APIDictionary.ContainsKey(dictionaryKey))
				{
					if(Gateway.GetDebugMode())
						Debug.WriteLine("Error - no known CRUD path for " + dictionaryKey);
					throw new Exception();
				}
				return APIDictionary[dictionaryKey];
            }

            /* All the basic CRUD: Hardcoded */

			/*
			 * Create a new instance of the model and persist it into the data source
			 */
            public static async Task<T> Create(T theModel)
            {
                String APIPath = getAPIPath("Create");
                var response = await Gateway.PerformPostRequest<T, T>(theModel, APIPath).ConfigureAwait(false);
                return response;
            }

			/*
			 * Update an existing model instance or insert a new one into the data source
			 */
            public static async Task<T> Upsert(T theModel)
            {
                String APIPath = getAPIPath("Upsert");
                var response = await Gateway.PerformPutRequest<T, T>(theModel, APIPath).ConfigureAwait(false);
                return response;
            }

			/*
			 * Check whether a model instance exists in the data source
			 */
            public static async Task<bool> Exists(string ID)
            {
                String APIPath = getAPIPath("Exists");
                APIPath = APIPath.Replace(":id", ID);
                var response = await Gateway.PerformGetRequest<object>(APIPath).ConfigureAwait(false);
                return JObject.Parse(response.ToString()).First.First.ToObject<bool>();
            }

			/*
			 * Find a model instance by id from the data source
			 */
            public static async Task<T> FindById(String ID)
            {
                String APIPath = getAPIPath("FindById");
                APIPath = APIPath.Replace(":id", ID);
                var response = await Gateway.PerformGetRequest<T>(APIPath).ConfigureAwait(false);
                return response;
            }

			/*
			 * Find all instances of the model matched by filter from the data source
			 */
            public static async Task<IList<T>> Find(string filter = "")
            {
                String APIPath = getAPIPath("Find");
                IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				queryStrings.Add("filter", filter);
                var response = await Gateway.PerformGetRequest<T[]>(APIPath, queryStrings).ConfigureAwait(false);
                return response.ToList();
            }

			/*
			 * Find first instance of the model matched by filter from the data source
			 */
            public static async Task<T> FindOne(string filter = "")
            {
                String APIPath = getAPIPath("FindOne");
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				queryStrings.Add("filter", filter);
                var response = await Gateway.PerformGetRequest<T>(APIPath, queryStrings).ConfigureAwait(false);
                return response;
            }

			/*
			 * Update instances of the model matched by where from the data source
			 */
            public static async Task UpdateAll(T updateModel, string whereFilter)
            {
				String APIPath = getAPIPath("UpdateAll");
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				queryStrings.Add("where", whereFilter);
                var response = await Gateway.PerformPostRequest<T, string>(updateModel, APIPath, queryStrings).ConfigureAwait(false);
            }

			/*
			 * Delete a model instance by id from the data source
			 */
            public static async Task DeleteById(String ID)
            {
				String APIPath = getAPIPath("DeleteById");
                APIPath = APIPath.Replace(":id", ID);
                var response = await Gateway.PerformRequest<string>(APIPath, "", "DELETE").ConfigureAwait(false);
            }

			/*
			 * Count instances of the model matched by where from the data source
			 */
            public static async Task<int> Count(string whereFilter = "")
            {
                String APIPath = getAPIPath("Count");
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				queryStrings.Add("where", whereFilter);
                var response = await Gateway.PerformGetRequest<object>(APIPath, queryStrings).ConfigureAwait(false);
                return JObject.Parse(response.ToString()).First.First.ToObject<int>();
            }

			/*
			 * Update attributes for a model instance and persist it into the data source
			 */
            public static async Task<T> UpdateById(String ID, T update)
            {
                String APIPath = getAPIPath("prototype$updateAttributes");
                APIPath = APIPath.Replace(":id", ID);
                var response = await Gateway.PerformPutRequest<T, T>(update, APIPath).ConfigureAwait(false);
                return response;
            }
        }

		// Dynamic repositories for all Dynamic models:
		public class Events : CRUDInterface<Event>
		{

			/*
			 * Fetches belongsTo relation user.
			 */
			public static async Task<User> getUser(string id, bool refresh = default(bool))
			{
				string APIPath = "events/:id/user";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Find a related item by id for participants.
			 */
			public static async Task<User> findByIdParticipants(string id, string fk)
			{
				string APIPath = "events/:id/participants/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Delete a related item by id for participants.
			 */
			public static async Task destroyByIdParticipants(string id, string fk)
			{
				string APIPath = "events/:id/participants/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Update a related item by id for participants.
			 */
			public static async Task<User> updateByIdParticipants(User data, string id, string fk)
			{
				string APIPath = "events/:id/participants/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Add a related item by id for participants.
			 */
			public static async Task<EventParticipant> linkParticipants(EventParticipant data, string id, string fk)
			{
				string APIPath = "events/:id/participants/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<EventParticipant>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Remove the participants relation to an item by id.
			 */
			public static async Task unlinkParticipants(string id, string fk)
			{
				string APIPath = "events/:id/participants/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Check the existence of participants relation to an item by id.
			 */
			public static async Task<bool> existsParticipants(string id, string fk)
			{
				string APIPath = "events/:id/participants/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<bool>(APIPath, bodyJSON, "HEAD", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Queries participants of event.
			 */
			public static async Task<IList<User>> getParticipants(string id, string filter = default(string))
			{
				string APIPath = "events/:id/participants";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("filter", filter != null ? filter.ToString() : null);
				var response = await Gateway.PerformRequest<User[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Creates a new instance in participants of this model.
			 */
			public static async Task<User> createParticipants(User data, string id)
			{
				string APIPath = "events/:id/participants";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Deletes all participants of this model.
			 */
			public static async Task deleteParticipants(string id)
			{
				string APIPath = "events/:id/participants";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Counts participants of event.
			 */
			public static async Task<double> countParticipants(string id, string where = default(string))
			{
				string APIPath = "events/:id/participants/count";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("where", where != null ? where.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<double>();
			}

			/*
			 * No description given.
			 */
			public static async Task<Array> getNearestEvents(string userId = default(string), string from = default(string), string to = default(string))
			{
				string APIPath = "events/nearestEvents";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("userId", userId != null ? userId.ToString() : null);
				queryStrings.Add("from", from != null ? from.ToString() : null);
				queryStrings.Add("to", to != null ? to.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<Array>();
			}

			/*
			 * Find a related item by id for events.
			 */
			public static async Task<Event> findByIdForuser(string id, string fk)
			{
				string APIPath = "users/:id/events/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<Event>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Delete a related item by id for events.
			 */
			public static async Task destroyByIdForuser(string id, string fk)
			{
				string APIPath = "users/:id/events/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Update a related item by id for events.
			 */
			public static async Task<Event> updateByIdForuser(Event data, string id, string fk)
			{
				string APIPath = "users/:id/events/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<Event>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Queries events of user.
			 */
			public static async Task<IList<Event>> getForuser(string id, string filter = default(string))
			{
				string APIPath = "users/:id/events";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("filter", filter != null ? filter.ToString() : null);
				var response = await Gateway.PerformRequest<Event[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Creates a new instance in events of this model.
			 */
			public static async Task<Event> createForuser(Event data, string id)
			{
				string APIPath = "users/:id/events";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<Event>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Deletes all events of this model.
			 */
			public static async Task deleteForuser(string id)
			{
				string APIPath = "users/:id/events";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Counts events of user.
			 */
			public static async Task<double> countForuser(string id, string where = default(string))
			{
				string APIPath = "users/:id/events/count";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("where", where != null ? where.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<double>();
			}
		}
		public class Users : CRUDInterface<User>
		{

			/*
			 * Find a related item by id for accessTokens.
			 */
			public static async Task<AccessToken> findByIdAccessTokens(string id, string fk)
			{
				string APIPath = "users/:id/accessTokens/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<AccessToken>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Delete a related item by id for accessTokens.
			 */
			public static async Task destroyByIdAccessTokens(string id, string fk)
			{
				string APIPath = "users/:id/accessTokens/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Update a related item by id for accessTokens.
			 */
			public static async Task<AccessToken> updateByIdAccessTokens(AccessToken data, string id, string fk)
			{
				string APIPath = "users/:id/accessTokens/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<AccessToken>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Find a related item by id for events.
			 */
			public static async Task<Event> findByIdEvents(string id, string fk)
			{
				string APIPath = "users/:id/events/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<Event>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Delete a related item by id for events.
			 */
			public static async Task destroyByIdEvents(string id, string fk)
			{
				string APIPath = "users/:id/events/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Update a related item by id for events.
			 */
			public static async Task<Event> updateByIdEvents(Event data, string id, string fk)
			{
				string APIPath = "users/:id/events/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<Event>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Find a related item by id for friendTo.
			 */
			public static async Task<User> findByIdFriendTo(string id, string fk)
			{
				string APIPath = "users/:id/friendTo/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Delete a related item by id for friendTo.
			 */
			public static async Task destroyByIdFriendTo(string id, string fk)
			{
				string APIPath = "users/:id/friendTo/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Update a related item by id for friendTo.
			 */
			public static async Task<User> updateByIdFriendTo(User data, string id, string fk)
			{
				string APIPath = "users/:id/friendTo/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Add a related item by id for friendTo.
			 */
			public static async Task<Friend> linkFriendTo(Friend data, string id, string fk)
			{
				string APIPath = "users/:id/friendTo/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<Friend>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Remove the friendTo relation to an item by id.
			 */
			public static async Task unlinkFriendTo(string id, string fk)
			{
				string APIPath = "users/:id/friendTo/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Check the existence of friendTo relation to an item by id.
			 */
			public static async Task<bool> existsFriendTo(string id, string fk)
			{
				string APIPath = "users/:id/friendTo/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<bool>(APIPath, bodyJSON, "HEAD", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Find a related item by id for myFriends.
			 */
			public static async Task<User> findByIdMyFriends(string id, string fk)
			{
				string APIPath = "users/:id/myFriends/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Delete a related item by id for myFriends.
			 */
			public static async Task destroyByIdMyFriends(string id, string fk)
			{
				string APIPath = "users/:id/myFriends/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Update a related item by id for myFriends.
			 */
			public static async Task<User> updateByIdMyFriends(User data, string id, string fk)
			{
				string APIPath = "users/:id/myFriends/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Add a related item by id for myFriends.
			 */
			public static async Task<Friend> linkMyFriends(Friend data, string id, string fk)
			{
				string APIPath = "users/:id/myFriends/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<Friend>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Remove the myFriends relation to an item by id.
			 */
			public static async Task unlinkMyFriends(string id, string fk)
			{
				string APIPath = "users/:id/myFriends/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Check the existence of myFriends relation to an item by id.
			 */
			public static async Task<bool> existsMyFriends(string id, string fk)
			{
				string APIPath = "users/:id/myFriends/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<bool>(APIPath, bodyJSON, "HEAD", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Queries accessTokens of user.
			 */
			public static async Task<IList<AccessToken>> getAccessTokens(string id, string filter = default(string))
			{
				string APIPath = "users/:id/accessTokens";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("filter", filter != null ? filter.ToString() : null);
				var response = await Gateway.PerformRequest<AccessToken[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Creates a new instance in accessTokens of this model.
			 */
			public static async Task<AccessToken> createAccessTokens(AccessToken data, string id)
			{
				string APIPath = "users/:id/accessTokens";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<AccessToken>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Deletes all accessTokens of this model.
			 */
			public static async Task deleteAccessTokens(string id)
			{
				string APIPath = "users/:id/accessTokens";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Counts accessTokens of user.
			 */
			public static async Task<double> countAccessTokens(string id, string where = default(string))
			{
				string APIPath = "users/:id/accessTokens/count";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("where", where != null ? where.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<double>();
			}

			/*
			 * Queries events of user.
			 */
			public static async Task<IList<Event>> getEvents(string id, string filter = default(string))
			{
				string APIPath = "users/:id/events";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("filter", filter != null ? filter.ToString() : null);
				var response = await Gateway.PerformRequest<Event[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Creates a new instance in events of this model.
			 */
			public static async Task<Event> createEvents(Event data, string id)
			{
				string APIPath = "users/:id/events";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<Event>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Deletes all events of this model.
			 */
			public static async Task deleteEvents(string id)
			{
				string APIPath = "users/:id/events";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Counts events of user.
			 */
			public static async Task<double> countEvents(string id, string where = default(string))
			{
				string APIPath = "users/:id/events/count";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("where", where != null ? where.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<double>();
			}

			/*
			 * Queries friendTo of user.
			 */
			public static async Task<IList<User>> getFriendTo(string id, string filter = default(string))
			{
				string APIPath = "users/:id/friendTo";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("filter", filter != null ? filter.ToString() : null);
				var response = await Gateway.PerformRequest<User[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Creates a new instance in friendTo of this model.
			 */
			public static async Task<User> createFriendTo(User data, string id)
			{
				string APIPath = "users/:id/friendTo";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Deletes all friendTo of this model.
			 */
			public static async Task deleteFriendTo(string id)
			{
				string APIPath = "users/:id/friendTo";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Counts friendTo of user.
			 */
			public static async Task<double> countFriendTo(string id, string where = default(string))
			{
				string APIPath = "users/:id/friendTo/count";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("where", where != null ? where.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<double>();
			}

			/*
			 * Queries myFriends of user.
			 */
			public static async Task<IList<User>> getMyFriends(string id, string filter = default(string))
			{
				string APIPath = "users/:id/myFriends";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("filter", filter != null ? filter.ToString() : null);
				var response = await Gateway.PerformRequest<User[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Creates a new instance in myFriends of this model.
			 */
			public static async Task<User> createMyFriends(User data, string id)
			{
				string APIPath = "users/:id/myFriends";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Deletes all myFriends of this model.
			 */
			public static async Task deleteMyFriends(string id)
			{
				string APIPath = "users/:id/myFriends";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Counts myFriends of user.
			 */
			public static async Task<double> countMyFriends(string id, string where = default(string))
			{
				string APIPath = "users/:id/myFriends/count";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("where", where != null ? where.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<double>();
			}

			/*
			 * Login a user with username/email and password.
			 */
			public static async Task<JObject> login(User credentials, string include = default(string))
			{
				string APIPath = "users/login";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(credentials);
				queryStrings.Add("include", include != null ? include.ToString() : null);
				var response = await Gateway.PerformRequest<JObject>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Logout a user with access token.
			 */
			public static async Task logout()
			{
				string APIPath = "users/logout";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Confirm a user registration with email verification token.
			 */
			public static async Task confirm(string uid = default(string), string token = default(string), string redirect = default(string))
			{
				string APIPath = "users/confirm";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("uid", uid != null ? uid.ToString() : null);
				queryStrings.Add("token", token != null ? token.ToString() : null);
				queryStrings.Add("redirect", redirect != null ? redirect.ToString() : null);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Reset password for a user with email.
			 */
			public static async Task resetPassword(User options)
			{
				string APIPath = "users/reset";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(options);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * No description given.
			 */
			public static async Task<Array> searchForUsers(string searchPhrase = default(string))
			{
				string APIPath = "users/searchForUsers";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("searchPhrase", searchPhrase != null ? searchPhrase.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<Array>();
			}

			/*
			 * Fetches belongsTo relation user.
			 */
			public static async Task<User> getForevent(string id, bool refresh = default(bool))
			{
				string APIPath = "events/:id/user";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Find a related item by id for participants.
			 */
			public static async Task<User> findByIdForevent(string id, string fk)
			{
				string APIPath = "events/:id/participants/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Delete a related item by id for participants.
			 */
			public static async Task destroyByIdForevent(string id, string fk)
			{
				string APIPath = "events/:id/participants/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Update a related item by id for participants.
			 */
			public static async Task<User> updateByIdForevent(User data, string id, string fk)
			{
				string APIPath = "events/:id/participants/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Add a related item by id for participants.
			 */
			public static async Task<EventParticipant> linkForevent(EventParticipant data, string id, string fk)
			{
				string APIPath = "events/:id/participants/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<EventParticipant>(APIPath, bodyJSON, "PUT", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Remove the participants relation to an item by id.
			 */
			public static async Task unlinkForevent(string id, string fk)
			{
				string APIPath = "events/:id/participants/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Check the existence of participants relation to an item by id.
			 */
			public static async Task<bool> existsForevent(string id, string fk)
			{
				string APIPath = "events/:id/participants/rel/:fk";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				APIPath = APIPath.Replace(":fk", (string)fk);
				var response = await Gateway.PerformRequest<bool>(APIPath, bodyJSON, "HEAD", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Queries participants of event.
			 */
			public static async Task<IList<User>> getForevent1(string id, string filter = default(string))
			{
				string APIPath = "events/:id/participants";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("filter", filter != null ? filter.ToString() : null);
				var response = await Gateway.PerformRequest<User[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Creates a new instance in participants of this model.
			 */
			public static async Task<User> createForevent(User data, string id)
			{
				string APIPath = "events/:id/participants";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(data);
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Deletes all participants of this model.
			 */
			public static async Task deleteForevent(string id)
			{
				string APIPath = "events/:id/participants";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * Counts participants of event.
			 */
			public static async Task<double> countForevent(string id, string where = default(string))
			{
				string APIPath = "events/:id/participants/count";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("where", where != null ? where.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<double>();
			}

			/*
			 * Fetches belongsTo relation friendTo.
			 */
			public static async Task<User> getForfriend(string id, bool refresh = default(bool))
			{
				string APIPath = "friends/:id/friendTo";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Fetches belongsTo relation friendOf.
			 */
			public static async Task<User> getForfriend1(string id, bool refresh = default(bool))
			{
				string APIPath = "friends/:id/friendOf";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Fetches belongsTo relation inviteeUser.
			 */
			public static async Task<User> getForfriendInvitation(string id, bool refresh = default(bool))
			{
				string APIPath = "friendInvitations/:id/inviteeUser";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Fetches belongsTo relation invitedByUser.
			 */
			public static async Task<User> getForfriendInvitation1(string id, bool refresh = default(bool))
			{
				string APIPath = "friendInvitations/:id/invitedByUser";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}
		}
		public class Participants : CRUDInterface<EventParticipant>
		{
		}
		public class Containers : CRUDInterface<Container>
		{

			/*
			 * No description given.
			 */
			public static async Task<IList<string>> getContainers()
			{
				string APIPath = "containers";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				var response = await Gateway.PerformRequest<string[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * No description given.
			 */
			public static async Task<JObject> createContainer(Container options)
			{
				string APIPath = "containers";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				bodyJSON = JsonConvert.SerializeObject(options);
				var response = await Gateway.PerformRequest<JObject>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * No description given.
			 */
			public static async Task destroyContainer(string container)
			{
				string APIPath = "containers/:container";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":container", (string)container);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * No description given.
			 */
			public static async Task<JObject> getContainer(string container)
			{
				string APIPath = "containers/:container";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":container", (string)container);
				var response = await Gateway.PerformRequest<JObject>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * No description given.
			 */
			public static async Task<IList<string>> getFiles(string container)
			{
				string APIPath = "containers/:container/files";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":container", (string)container);
				var response = await Gateway.PerformRequest<string[]>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * No description given.
			 */
			public static async Task<JObject> getFile(string container, string file)
			{
				string APIPath = "containers/:container/files/:file";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":container", (string)container);
				APIPath = APIPath.Replace(":file", (string)file);
				var response = await Gateway.PerformRequest<JObject>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * No description given.
			 */
			public static async Task removeFile(string container, string file)
			{
				string APIPath = "containers/:container/files/:file";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":container", (string)container);
				APIPath = APIPath.Replace(":file", (string)file);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "DELETE", queryStrings).ConfigureAwait(false);
				
			}

			/*
			 * No description given.
			 */
			public static async Task<JObject> upload(string req, string res)
			{
				string APIPath = "containers/:container/upload";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "POST", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<JObject>();
			}

			/*
			 * No description given.
			 */
			public static async Task download(string container, string file, string req, string res)
			{
				string APIPath = "containers/:container/download/:file";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":container", (string)container);
				APIPath = APIPath.Replace(":file", (string)file);
				var response = await Gateway.PerformRequest<string>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				
			}
		}
		public class Friends : CRUDInterface<Friend>
		{

			/*
			 * Fetches belongsTo relation friendTo.
			 */
			public static async Task<User> getFriendTo(string id, bool refresh = default(bool))
			{
				string APIPath = "friends/:id/friendTo";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Fetches belongsTo relation friendOf.
			 */
			public static async Task<User> getFriendOf(string id, bool refresh = default(bool))
			{
				string APIPath = "friends/:id/friendOf";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * No description given.
			 */
			public static async Task<string> removeFromFriends(string firstUserId = default(string), string secondUserId = default(string))
			{
				string APIPath = "friends/removeFromFriends";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("firstUserId", firstUserId != null ? firstUserId.ToString() : null);
				queryStrings.Add("secondUserId", secondUserId != null ? secondUserId.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<string>();
			}

			/*
			 * No description given.
			 */
			public static async Task<Array> getFriends(string userId = default(string))
			{
				string APIPath = "friends/getFriends";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("userId", userId != null ? userId.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<Array>();
			}
		}
		public class FriendInvitations : CRUDInterface<FriendInvitation>
		{

			/*
			 * Fetches belongsTo relation inviteeUser.
			 */
			public static async Task<User> getInviteeUser(string id, bool refresh = default(bool))
			{
				string APIPath = "friendInvitations/:id/inviteeUser";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * Fetches belongsTo relation invitedByUser.
			 */
			public static async Task<User> getInvitedByUser(string id, bool refresh = default(bool))
			{
				string APIPath = "friendInvitations/:id/invitedByUser";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				APIPath = APIPath.Replace(":id", (string)id);
				queryStrings.Add("refresh", refresh != null ? refresh.ToString() : null);
				var response = await Gateway.PerformRequest<User>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return response;
			}

			/*
			 * No description given.
			 */
			public static async Task<Array> getUserInvitations(string userId = default(string))
			{
				string APIPath = "friendInvitations/getUserInvitations";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("userId", userId != null ? userId.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<Array>();
			}

			/*
			 * No description given.
			 */
			public static async Task<string> acceptFriendship(string invitationId = default(string))
			{
				string APIPath = "friendInvitations/acceptFriendship";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("invitationId", invitationId != null ? invitationId.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<string>();
			}

			/*
			 * No description given.
			 */
			public static async Task<JObject> inviteFriend(string invitedBy = default(string), string invitee = default(string))
			{
				string APIPath = "friendInvitations/inviteFriend";
				IDictionary<string, string> queryStrings = new Dictionary<string, string>();
				string bodyJSON = "";
				queryStrings.Add("invitedBy", invitedBy != null ? invitedBy.ToString() : null);
				queryStrings.Add("invitee", invitee != null ? invitee.ToString() : null);
				var response = await Gateway.PerformRequest<object>(APIPath, bodyJSON, "GET", queryStrings).ConfigureAwait(false);
				return JObject.Parse(response.ToString()).First.First.ToObject<JObject>();
			}
		}
		
	}
}

/**
 *** Dynamic Models ***
 */

namespace LBXamarinSDK
{
	public partial class Event : LBModel
	{
		[JsonProperty ("title", NullValueHandling = NullValueHandling.Ignore)]
		public String title { get; set; }

		[JsonIgnore]
		public DateTime from
		{
			get { return _from ?? new DateTime(); }
			set { _from = value; }
		}
		[JsonProperty ("from", NullValueHandling = NullValueHandling.Ignore)]
		private DateTime? _from { get; set; }

		[JsonIgnore]
		public DateTime to
		{
			get { return _to ?? new DateTime(); }
			set { _to = value; }
		}
		[JsonProperty ("to", NullValueHandling = NullValueHandling.Ignore)]
		private DateTime? _to { get; set; }

		[JsonProperty ("description", NullValueHandling = NullValueHandling.Ignore)]
		public String description { get; set; }

		[JsonProperty ("type", NullValueHandling = NullValueHandling.Ignore)]
		public String type { get; set; }

		[JsonProperty ("id", NullValueHandling = NullValueHandling.Ignore)]
		public string id { get; set; }

		[JsonProperty ("userId", NullValueHandling = NullValueHandling.Ignore)]
		public String userId { get; set; }

		
		// This method identifies the ID field
		public override string getID()
		{
			return id;
		}
	}
	public partial class User : LBModel
	{
		[JsonProperty ("firstName", NullValueHandling = NullValueHandling.Ignore)]
		public String firstName { get; set; }

		[JsonProperty ("lastName", NullValueHandling = NullValueHandling.Ignore)]
		public String lastName { get; set; }

		[JsonProperty ("login", NullValueHandling = NullValueHandling.Ignore)]
		public String login { get; set; }

		[JsonProperty ("photoUrl", NullValueHandling = NullValueHandling.Ignore)]
		public String photoUrl { get; set; }

		[JsonProperty ("realm", NullValueHandling = NullValueHandling.Ignore)]
		public String realm { get; set; }

		[JsonProperty ("username", NullValueHandling = NullValueHandling.Ignore)]
		public String username { get; set; }

		[JsonProperty ("password", NullValueHandling = NullValueHandling.Ignore)]
		public String password { get; set; }

		[JsonProperty ("credentials", NullValueHandling = NullValueHandling.Ignore)]
		public Object credentials { get; set; }

		[JsonProperty ("challenges", NullValueHandling = NullValueHandling.Ignore)]
		public Object challenges { get; set; }

		[JsonProperty ("email", NullValueHandling = NullValueHandling.Ignore)]
		public String email { get; set; }

		[JsonIgnore]
		public bool emailVerified
		{
			get { return _emailVerified ?? new bool(); }
			set { _emailVerified = value; }
		}
		[JsonProperty ("emailVerified", NullValueHandling = NullValueHandling.Ignore)]
		private bool? _emailVerified { get; set; }

		[JsonProperty ("verificationToken", NullValueHandling = NullValueHandling.Ignore)]
		public String verificationToken { get; set; }

		[JsonProperty ("status", NullValueHandling = NullValueHandling.Ignore)]
		public String status { get; set; }

		[JsonIgnore]
		public DateTime created
		{
			get { return _created ?? new DateTime(); }
			set { _created = value; }
		}
		[JsonProperty ("created", NullValueHandling = NullValueHandling.Ignore)]
		private DateTime? _created { get; set; }

		[JsonIgnore]
		public DateTime lastUpdated
		{
			get { return _lastUpdated ?? new DateTime(); }
			set { _lastUpdated = value; }
		}
		[JsonProperty ("lastUpdated", NullValueHandling = NullValueHandling.Ignore)]
		private DateTime? _lastUpdated { get; set; }

		[JsonProperty ("id", NullValueHandling = NullValueHandling.Ignore)]
		public string id { get; set; }

		
		// This method identifies the ID field
		public override string getID()
		{
			return id;
		}
	}
	public partial class EventParticipant : LBModel
	{
		[JsonIgnore]
		public double eventId
		{
			get { return _eventId ?? new double(); }
			set { _eventId = value; }
		}
		[JsonProperty ("eventId", NullValueHandling = NullValueHandling.Ignore)]
		private double? _eventId { get; set; }

		[JsonIgnore]
		public double userId
		{
			get { return _userId ?? new double(); }
			set { _userId = value; }
		}
		[JsonProperty ("userId", NullValueHandling = NullValueHandling.Ignore)]
		private double? _userId { get; set; }

		[JsonIgnore]
		public bool attend
		{
			get { return _attend ?? new bool(); }
			set { _attend = value; }
		}
		[JsonProperty ("attend", NullValueHandling = NullValueHandling.Ignore)]
		private bool? _attend { get; set; }

		[JsonProperty ("id", NullValueHandling = NullValueHandling.Ignore)]
		public string id { get; set; }

		
		// This method identifies the ID field
		public override string getID()
		{
			return id;
		}
	}
	public partial class Container : LBModel
	{
		[JsonProperty ("id", NullValueHandling = NullValueHandling.Ignore)]
		public string id { get; set; }

		
		// This method identifies the ID field
		public override string getID()
		{
			return id;
		}
	}
	public partial class Friend : LBModel
	{
		[JsonProperty ("friendOfId", NullValueHandling = NullValueHandling.Ignore)]
		public String friendOfId { get; set; }

		[JsonProperty ("friendToId", NullValueHandling = NullValueHandling.Ignore)]
		public String friendToId { get; set; }

		[JsonProperty ("id", NullValueHandling = NullValueHandling.Ignore)]
		public string id { get; set; }

		
		// This method identifies the ID field
		public override string getID()
		{
			return id;
		}
	}
	public partial class FriendInvitation : LBModel
	{
		[JsonProperty ("inviteeId", NullValueHandling = NullValueHandling.Ignore)]
		public String inviteeId { get; set; }

		[JsonProperty ("invitedById", NullValueHandling = NullValueHandling.Ignore)]
		public String invitedById { get; set; }

		[JsonProperty ("response", NullValueHandling = NullValueHandling.Ignore)]
		public String response { get; set; }

		[JsonIgnore]
		public DateTime createdOn
		{
			get { return _createdOn ?? new DateTime(); }
			set { _createdOn = value; }
		}
		[JsonProperty ("createdOn", NullValueHandling = NullValueHandling.Ignore)]
		private DateTime? _createdOn { get; set; }

		[JsonIgnore]
		public DateTime updatedOn
		{
			get { return _updatedOn ?? new DateTime(); }
			set { _updatedOn = value; }
		}
		[JsonProperty ("updatedOn", NullValueHandling = NullValueHandling.Ignore)]
		private DateTime? _updatedOn { get; set; }

		[JsonProperty ("id", NullValueHandling = NullValueHandling.Ignore)]
		public string id { get; set; }

		
		// This method identifies the ID field
		public override string getID()
		{
			return id;
		}
	}

	// Relationship classes:
	// None.
}
// Eof
