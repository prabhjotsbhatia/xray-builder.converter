/*   Builds an X-Ray file to be used on the Amazon Kindle
*   Original xray builder by shinew, http://www.mobileread.com/forums/showthread.php?t=157770 , http://www.xunwang.me/xray/
*   
*   Copyright (C) 2014 Ephemerality <Nick Niemi - ephemeral.vilification@gmail.com>
*
*   This program is free software: you can redistribute it and/or modify
*   it under the terms of the GNU General Public License as published by
*   the Free Software Foundation, either version 3 of the License, or
*   (at your option) any later version.

*   This program is distributed in the hope that it will be useful,
*   but WITHOUT ANY WARRANTY; without even the implied warranty of
*   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*   GNU General Public License for more details.

*   You should have received a copy of the GNU General Public License
*   along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

// HTMLAgilityPack from http://htmlagilitypack.codeplex.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Serialization;
using Newtonsoft.Json;
using System.Data.SQLite;

namespace XRay2Converter
{
    public class XRayDef
    {
        public string asin;
        public string guid;
        public string version;
        public string xrayversion;
        public string created;
        public IList<XTerm> terms;
        public IList<XChapter> chapters;
        public XAssets assets;
        public int srl;
        public int erl;
    }

    public class XTerm
    {
        public string type;
        public string term;
        public string desc;
        public string descSrc;
        public string descUrl;
        public IList<IList<int>> locs;
        public int id;
    }

    public class XChapter
    {
        public string name;
        public int start;
        public int end;
    }

    public class XAssets
    {

    }

    public class NewXRay
    {
        XRayDef xray;
        public NewXRay()
        {
        }

        public NewXRay(string asc)
        {
            if (asc == "")
                throw new ArgumentException("Error initializing X-Ray, one of the required parameters was blank.");
            string readContents;
            using (StreamReader streamReader = new StreamReader(asc, Encoding.UTF8))
            {
                readContents = streamReader.ReadToEnd();
            }
            xray = JsonConvert.DeserializeObject<XRayDef>(readContents);
        }

        public void PopulateDB(SQLiteConnection db, string shelfariURL)
        {
            int termCount = 0;
            int personCount = 0;
            int entity = 1;
            int excerpt = 0;
            string sql = "";
            SQLiteCommand command;
            command = new SQLiteCommand(db);
            command.CommandText = "update string set text=@text where id=15";
            command.Parameters.AddWithValue("text", shelfariURL);
            command.ExecuteNonQuery();
            Console.WriteLine("Updating database with terms, descriptions, and excerpts...");
            foreach (XTerm t in xray.terms)
            {
                try
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                }
                catch (Exception ex)
                {
                }
                Console.Write("Writing term {0} of {1} ({2})          ", entity, xray.terms.Count, ((double)(entity - 1.0)  / xray.terms.Count).ToString("##.0%"));
                command = new SQLiteCommand(db);
                if (t.type == "character") personCount++;
                else if (t.type == "topic") termCount++;
                command.CommandText = String.Format("insert into entity (id, label, loc_label, type, count, has_info_card) values ({0}, @label, null, {1}, {2}, 1);",
                    entity, t.type == "character" ? 1 : 2, t.locs.Count);
                command.Parameters.AddWithValue("label", t.term);
                command.ExecuteNonQuery();

                command = new SQLiteCommand(db);
                command.CommandText = String.Format("insert into entity_description (text, source_wildcard, source, entity) values (@text, @source_wildcard, {0}, {1});",
                    t.descSrc == "shelfari" ? 2 : 4, entity);
                command.Parameters.AddWithValue("text", t.desc);
                command.Parameters.AddWithValue("source_wildcard", t.term);
                command.ExecuteNonQuery();

                sql = "";
                foreach (IList<int> loc in t.locs)
                {
                    sql += String.Format("insert into excerpt (id, start, length, image, related_entities, goto) values ({0}, {1}, {2}, null, '{3}', null);\n",
                        excerpt, loc[0], loc[1], entity);
                    sql += String.Format("insert into entity_excerpt (entity, excerpt) values ({0}, {1});\n", entity, excerpt);
                    sql += String.Format("insert into occurrence (entity, start, length) values ({0}, {1}, {2});\n", entity, loc[2], loc[3]);
                    excerpt++;
                }
                command = new SQLiteCommand(sql, db);
                command.ExecuteNonQuery();
                t.id = entity++;
            }
            Console.WriteLine("\nWriting top mentions...");
            List<int> sorted = xray.terms.Where<XTerm>(t=>t.type.Equals("character")).OrderByDescending(t=>t.locs.Count).Select(t=>t.id).ToList<int>();
            sql = String.Format("update type set top_mentioned_entities='{0}' where id=1;\n",
                String.Join(",", sorted.GetRange(0, Math.Min(10, sorted.Count))));
            sorted = xray.terms.Where<XTerm>(t => t.type.Equals("topic")).OrderByDescending(t => t.locs.Count).Select(t => t.id).ToList<int>();
            sql += String.Format("update type set top_mentioned_entities='{0}' where id=2;",
                String.Join(",", sorted.GetRange(0, Math.Min(10, sorted.Count))));
            command = new SQLiteCommand(sql, db);
            command.ExecuteNonQuery();

            Console.WriteLine("Writing metadata...");
            sql = String.Format("insert into book_metadata (srl, erl, has_images, has_excerpts, show_spoilers_default, num_people, num_terms, num_images, preview_images) " 
                + "values ({0}, {1}, 0, 1, 0, {2}, {3}, 0, null);", xray.srl, xray.erl, personCount, termCount);
            
            command = new SQLiteCommand(sql, db);
            int result2 = command.ExecuteNonQuery();
            SQLiteErrorCode est = db.ExtendedResultCode();
        }
    }

    class Chapter
    {
        public string name;
        public long start;
        public long end;

        public Chapter()
        {
            this.name = "";
            this.start = 1;
            this.end = 9999999;
        }

        public Chapter(string name, long start, long end)
        {
            this.name = name;
            this.start = start;
            this.end = end;
        }

        public override string ToString()
        {
            return String.Format(@"{{""name"":{0},""start"":{1},""end"":{2}}}", (name == "" ? "null" : "\"" + name + "\""), start, end);
        }
    }

    public class Term
    {
        public string type = "";
        [XmlElement("name")]
        public string termName = "";
        public string desc = "";
        [XmlElement("src")]
        public string descSrc = "";
        [XmlElement("url")]
        public string descUrl = "";
        [XmlIgnore]
        public List<string> aliases = new List<string>();
        [XmlIgnore]
        public List<string> locs = new List<string>(1000);
        [XmlIgnore]
        public List<string> assets = new List<string> { "" };
        public bool matchCase = true;

        public Term() { }

        public Term(string type)
        {
            this.type = type;
        }

        public override string ToString()
        {
            //Note that the Amazon X-Ray files declare an "assets" var for each term, but I have not seen one that actually uses them to contain anything
            if (locs.Count > 0)
                return String.Format(@"{{""type"":""{0}"",""term"":""{1}"",""desc"":""{2}"",""descSrc"":""{3}"",""descUrl"":""{4}"",""locs"":[{5}]}}", //,""assets"":[{6}]}}",
                    type, termName, desc, descSrc, descUrl, string.Join(",", locs));
            else
            {
                return String.Format(@"{{""type"":""{0}"",""term"":""{1}"",""desc"":""{2}"",""descSrc"":""{3}"",""descUrl"":""{4}"",""locs"":[[100,100,100,6]]}}", //,""assets"":[{6}]}}",
                    type, termName, desc, descSrc, descUrl);
            }
        }

    }
}
