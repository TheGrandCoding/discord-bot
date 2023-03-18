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
        public static APIErrorResponse InvalidQueryParams()
        {
            return new APIErrorResponse("", "Invalid Query Paramaters");
        }



        public APIErrorResponse WithError(string code, string message)
        {
            Errors.Add(new APIErrorResponse(code, message));
            return this;
        }
        public APIErrorResponse EndError(string code, string message)
        {
            WithError(code, message);
            return Build();
        }
        public APIErrorResponse WithRequired(string customText = null) => WithError("REQUIRED", "This field is required" + (customText == null ? "" : (": " + customText)));
        public APIErrorResponse WithChoices(params string[] possibleV) => WithError("CHOICES", "Value must be one of '" + string.Join("', '", possibleV) + "'");
        public APIErrorResponse WithRange(int v1, int v2) => WithError("RANGE", $"Value must be between {v1} and {v2}");

        public APIErrorResponse EndRequired(string customText = null)
        {
            WithRequired(customText);
            return Build();
        }
        public APIErrorResponse EndChoices(params string[] possibleV)
        {
            WithChoices(possibleV);
            return Build();
        }
        public APIErrorResponse EndRange(int v1, int v2)
        {
            WithRange(v1, v2);
            return Build();
        }


        private string _parentKey;
        public APIErrorResponse Child(string key)
        {
            var c = new APIErrorResponse(this, null, null);
            c._parentKey = key;
            Children[key] = c;
            return c;
        }
        public APIErrorResponse Child(int key) => Child(key.ToString());

        public APIErrorResponse this[int i] => Child(i);
        public APIErrorResponse this[string key] => Child(key);

        public APIErrorResponse Extend(APIErrorResponse child)
        {
            if (child == null) return this;
            foreach (var err in child.Errors)
                WithError(err.Code, err.Message);
            foreach((var key, var val) in child.Children)
            {
                val._parentKey = key;
                Children[key] = val;
            }
            return this;
        }


        public APIErrorResponse Build()
        {
            if(_parent != null)
            {
                _parent.Children[_parentKey] = this;
                return _parent.Build();
            }
            Prune();
            return this;
        }
        public bool Prune()
        {
            var remove = new List<string>();
            foreach ((var key, var child) in Children)
            {
                if (child.Prune())
                    remove.Add(key);
            }
            foreach(var x in remove)
                Children.Remove(x);
            return Children.Count == 0 && Errors.Count == 0;
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

        internal bool HasAnyErrors()
        {
            return Errors.Count > 0 || Children.Values.Any(x => x.HasAnyErrors());
        }
    }
}
