using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    public class APIErrorResponse
    {
        public APIErrorResponse() { }
        private APIErrorResponse(string code, string message)
        {
            Code = code;
            Message = message;
        }
        private APIErrorResponse(APIErrorResponse parent, string code, string message)  : this(code, message)
        {
            _parent = parent;
        }
        public string Message { get; set; }
        public string Code { get; set; }


        public Dictionary<string, APIErrorResponse> Children { get; set; } = new();

        public List<APIErrorResponse> Errors { get; set; } = new();

        private APIErrorResponse _parent;

        public static APIErrorResponse InvalidFormBody()
        {
            return new APIErrorResponse("", "Invalid Form Body");
        }

        public APIErrorResponse EndError(string code, string message)
        {
            Errors.Add(new APIErrorResponse(code, message));
            return Build();
        }
        public APIErrorResponse EndRequired() => EndError("REQUIRED", "This field is required");
        public APIErrorResponse EndChoices(params string[] possibleV) => EndError("CHOICES", "Value must be one of '" + string.Join("', '", possibleV) + "'");
        private string _parentKey;
        public APIErrorResponse Child(string key)
        {
            var c = new APIErrorResponse(this, null, null);
            c._parentKey = key;
            //Children[key] = c;
            return c;
        }
        public APIErrorResponse Child(int key) => Child(key.ToString());
        public APIErrorResponse Build()
        {
            if(_parent != null)
            {
                _parent.Children[_parentKey] = this;
                return _parent.Build();
            }
            return this;
        }

        public JObject ToJson()
        {
            var jobj = new JObject();
            bool rootNode = _parent == null;
            if(Children.Count > 0)
            {
                JObject addingTo = rootNode ? new JObject() : jobj;
                foreach(var keypair in Children)
                {
                    addingTo[keypair.Key] = keypair.Value.ToJson();
                }
                if (rootNode) jobj["errors"] = addingTo;
            } else if(Errors.Count > 0)
            {
                var jarr = new JArray();
                foreach(var error in Errors)
                    jarr.Add(error.ToJson());
                jobj["_errors"] = jarr;
            }
            if (!string.IsNullOrWhiteSpace(Code))
                jobj["code"] = Code;
            if(!string.IsNullOrWhiteSpace(Message))
                jobj["message"] = Message;
            return jobj;

        }
        public override string ToString() => ToJson().ToString(Newtonsoft.Json.Formatting.Indented);

    }
}
