using System;
using SimpleJSON;

namespace Klever.VR
{
    // Accept both populated legacy records and current API records. No backend
    // or authoring data is changed by these readers.
    public static class ContentCompatibility
    {
        // Keep parsing in the application's SimpleJSON assembly. This project
        // also contains a legacy firstpass copy, ambiguous to Editor scripts.
        public static JSONNode Parse(string json) => JSONNode.Parse(json);

        public static JSONArray Assessments(JSONNode experience)
        {
            var plural = experience["assessments"].AsArray;
            var singular = experience["assessment"].AsArray;
            return plural != null && plural.Count > 0 ? plural : singular ?? plural;
        }

        public static JSONNode VideoRecord(JSONNode detail)
        {
            if (detail["VideoId"].IsObject) return detail["VideoId"];
            if (detail["videoDetail"].IsObject) return detail["videoDetail"];
            return detail;
        }

        public static string VideoUrl(JSONNode detail)
        {
            foreach (var value in new[] { detail["videoURL"].Value, VideoRecord(detail)["videoURL"].Value,
                         detail["VideoId"].IsString ? detail["VideoId"].Value : "" })
                if (!string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                    return value.Trim();
            return "";
        }
    }
}
