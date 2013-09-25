using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Http;
using AutoMapper;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Mvc;
using Umbraco.Web.Routing;
using umbraco;

namespace Umbraco.Web.Editors
{
    /// <summary>
    /// API controller to deal with Macro data
    /// </summary>
    [PluginController("UmbracoApi")]
    public class MacroController : UmbracoAuthorizedJsonController
    {
        /// <summary>
        /// Gets the macro parameters to be filled in for a particular macro
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Note that ALL logged in users have access to this method because editors will need to isnert macros into rte (content/media/members) and it's used for 
        /// inserting into templates/views/etc... it doesn't expose any sensitive data.
        /// </remarks>
        public IEnumerable<MacroParameter> GetMacroParameters(int macroId)
        {
            var macro = Services.MacroService.GetById(macroId);
            if (macro == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            return Mapper.Map<IEnumerable<MacroParameter>>(macro);
        }

        /// <summary>
        /// Gets a rendered macro as html for rendering in the rich text editor
        /// </summary>
        /// <param name="macroAlias"></param>
        /// <param name="pageId"></param>
        /// <param name="macroParams">
        /// To send a dictionary as a GET parameter the query should be structured like:
        /// 
        /// ?macroAlias=Test&pageId=3634&macroParams[0].key=myKey&macroParams[0].value=myVal&macroParams[1].key=anotherKey&macroParams[1].value=anotherVal
        /// 
        /// </param>
        /// <returns></returns>
        public HttpResponseMessage GetMacroResultAsHtmlForEditor(string macroAlias, int pageId, [FromUri]IDictionary<string, object> macroParams)
        {
            var doc = Services.ContentService.GetById(pageId);
            if (doc == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            //need to get a legacy macro object - eventually we'll have a new format but nto yet
            var macro = new macro(macroAlias);
            if (macro == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            //if it isn't supposed to be rendered in the editor then return an empty string
            if (macro.DontRenderInEditor)
            {
                return new HttpResponseMessage()
                    {
                        //need to create a specific content result formatted as html since this controller has been configured
                        //with only json formatters.
                        Content = new StringContent(string.Empty, Encoding.UTF8, "text/html")
                    };
            }

            //because macro's are filled with insane legacy bits and pieces we need all sorts of wierdness to make them render.
            //the 'easiest' way might be to create an IPublishedContent manually and populate the legacy 'page' object with that
            //and then set the legacy parameters.

            var xml = doc.ToXml();
            var publishedContent = new XmlPublishedContent(xml.ToXmlElement());
            
            var legacyPage = new global::umbraco.page(publishedContent);                    
            UmbracoContext.HttpContext.Items["pageID"] = doc.Id;
            UmbracoContext.HttpContext.Items["pageElements"] = legacyPage.Elements;
            UmbracoContext.HttpContext.Items[global::Umbraco.Core.Constants.Conventions.Url.AltTemplate] = null;

            return new HttpResponseMessage()
            {
                //need to create a specific content result formatted as html since this controller has been configured
                //with only json formatters.
                Content = new StringContent(
                    Umbraco.RenderMacro(macro, macroParams, legacyPage).ToString(),
                    Encoding.UTF8,
                    "text/html"
                )
            };
        }

    }
}