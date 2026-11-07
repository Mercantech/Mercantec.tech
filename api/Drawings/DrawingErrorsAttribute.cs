using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mercantec.Tech.Api.Drawings;

public sealed class DrawingErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is DrawingException drawing)
        { context.Result = new ObjectResult(new { error = drawing.Code, message = drawing.Message }) { StatusCode = drawing.Status }; context.ExceptionHandled = true; }
        else if (context.Exception is HttpRequestException)
        { context.Result = new ObjectResult(new { error = "drawing_service_unavailable", message = "Tegningstjenesten er ikke tilgængelig. Prøv igen om lidt." }) { StatusCode = 503 }; context.ExceptionHandled = true; }
    }
}
