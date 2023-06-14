using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Photon.JobSeeker;

public interface IViewRenderService
{
    Task<string> RenderToStringAsync(string viewName, object model);
}

public class ViewRenderService : IViewRenderService
{
    private readonly IRazorViewEngine razor_view_engine;
    private readonly ITempDataProvider temp_data_provider;
    private readonly IServiceProvider service_provider;

    public ViewRenderService(
        IRazorViewEngine razorViewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider)
    {
        this.razor_view_engine = razorViewEngine;
        this.temp_data_provider = tempDataProvider;
        this.service_provider = serviceProvider;
    }

    public async Task<string> RenderToStringAsync(string viewName, object model)
    {
        var httpContext = new DefaultHttpContext { RequestServices = service_provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        using (var writer = new StringWriter())
        {
            var viewResult = razor_view_engine.FindView(actionContext, viewName, false);

            if (viewResult.View == null)
            {
                throw new ArgumentNullException($"{viewName} does not match any available view");
            }

            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, temp_data_provider),
                writer,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return writer.ToString();
        }
    }
}
