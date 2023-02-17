using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{

    public class FilterLists : AuthedAPIBase
    {
        const string FilterIdRegex = "[a-zA-Z0-9]{8,}";
        public FilterLists(APIContext context) : base(context, "filters")
        {
            Service = Program.Services.GetRequiredService<FilterListService>();
        }
        public FilterListService Service { get; set; }

        [Method("GET"), Path("/filters")]
        public void ListFilters()
        {
            var folder = Service.GetDirectory(Context.User.Id);

            var ul = new UnorderedList();
            foreach(var file in Directory.EnumerateFiles(folder))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var anchor = new Anchor($"/filters/{id}");
                anchor.RawText = id;
                ul.AddItem(new ListItem() { Children = { anchor } });
            }
            var na = new Anchor($"#new", id: "new");
            na.RawText = "[Add new]";
            ul.AddItem(new ListItem() { Children = { na } });

            ReplyFile("list.html", 200, new Replacements().Add("list", ul));
        }

        [Method("POST"), Path("/filters/new")]
        public void NewFilter()
        {
            if(!Service.TryCreateNew(Context.User.Id, out var id, out var fs))
            {
                RespondRaw("Failed", 400);
                return;
            }
            try
            {
                fs.Write(Encoding.UTF8.GetBytes("New file."));
            } finally
            {
                fs.Close();
            }
            RespondRedirect($"/filters/{id}");
        }

        [Method("GET"), Path("/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public void EditFilter(string filterId)
        {
            ReplyFile("edit.html", 200);
        }

        [Method("DELETE"), Path("/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public void DeleteFilter(string filterId)
        {
            if(Service.TryDelete(Context.User.Id, filterId))
            {
                RespondRedirect("/filters");
            } else
            {
                RespondRaw("Failed to delete", 500);
            }
        }

        [Method("GET"), Path("/filters-raw/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        [RequireNoExcessQuery(false)]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        public void FetchRaw(string filterId)
        {
            if(!Service.TryOpenRead(filterId, out var fs))
            {
                RespondRaw("No filter exists by that ID", 404);
                return;
            }
            try
            {
                StatusSent = 200;
                Context.HTTP.Response.StatusCode = 200;
                fs.CopyTo(Context.HTTP.Response.OutputStream);
                Context.HTTP.Response.Close();
            } finally
            {
                fs.Close();
            }
        }

        [Method("POST"), Path("/filters-raw/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public void UploadRaw(string filterId, bool append = false)
        {
            if(!Service.TryOpenWrite(Context.User.Id, filterId, out var fs))
            {
                RespondRaw("No filter exists by that ID or could not open file", 404);
                return;
            }
            try
            {
                var data = Encoding.UTF8.GetBytes(Context.Body);
                fs.Write(data);
            }
            finally
            {
                fs.Close();
            }
            RespondRaw("OK");
        }
    }
}
