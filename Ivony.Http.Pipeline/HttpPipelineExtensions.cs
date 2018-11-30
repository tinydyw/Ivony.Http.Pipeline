﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Internal;
using Microsoft.AspNetCore.Http;

namespace Ivony.Http.Pipeline
{


  /// <summary>
  /// 提供 HTTP 请求管线的帮助方法
  /// </summary>
  public static class HttpPipelineExtensions
  {


    /// <summary>
    /// 接入一个管线中间件
    /// </summary>
    /// <param name="middleware">上游管线</param>
    /// <param name="nextMiddleware">要接入的中间件</param>
    /// <returns>请求处理管线</returns>
    public static IHttpPipelineMiddleware Pipe( this IHttpPipelineMiddleware middleware, IHttpPipelineMiddleware nextMiddleware )
    {
      return new HttpPipelineMiddlewareLink( middleware, nextMiddleware );
    }


    /// <summary>
    /// 接入一个管线中间件
    /// </summary>
    /// <param name="middleware">上游管线</param>
    /// <param name="nextMiddleware">要接入的中间件</param>
    /// <returns>请求处理管线</returns>
    public static IHttpPipelineMiddleware Pipe( this IHttpPipelineMiddleware middleware, Func<Func<HttpRequestMessage, Task<HttpResponseMessage>>, Func<HttpRequestMessage, Task<HttpResponseMessage>>> next )
    {
      return new HttpPipelineMiddlewareLink( middleware, new Middleware( next ) );
    }


    private class Middleware : IHttpPipelineMiddleware
    {
      private readonly Func<Func<HttpRequestMessage, Task<HttpResponseMessage>>, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _middleware;

      public Middleware( Func<Func<HttpRequestMessage, Task<HttpResponseMessage>>, Func<HttpRequestMessage, Task<HttpResponseMessage>>> middleware )
      {
        _middleware = middleware;
      }

      public IHttpPipeline Pipe( IHttpPipeline pipeline )
      {
        return new Pipeline( _middleware( pipeline.ProcessRequest ) );
      }
    }

    private class Pipeline : IHttpPipeline
    {
      private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _func;

      public Pipeline( Func<HttpRequestMessage, Task<HttpResponseMessage>> func )
      {
        _func = func;
      }

      public Task<HttpResponseMessage> ProcessRequest( HttpRequestMessage request )
      {
        return _func( request );
      }
    }


    /// <summary>
    /// 使用负载均衡器
    /// </summary>
    /// <param name="middleware">上游管线</param>
    /// <param name="pipelines">下游管线列表</param>
    /// <returns>请求处理管线</returns>
    public static IHttpPipeline UseLoadBalancer( this IHttpPipelineMiddleware middleware, params Func<IHttpPipelineMiddleware, IHttpPipeline>[] pipelines )
    {
      return middleware.Pipe( new HttpPipelineDispatcher( pipelines.Select( func => func( new HttpPipelineBuilder() ) ).ToArray() ) );
    }


    /// <summary>
    /// 使用代理转发
    /// </summary>
    /// <param name="middleware">上游管线</param>
    /// <returns>请求处理管线</returns>
    public static IHttpPipelineMiddleware UseForwardedProxy( this IHttpPipelineMiddleware middleware )
    {
      return middleware.Pipe( new HttpPipelineForwardedProxy() );
    }


    public static HttpContext GetHttpContext( this HttpRequestMessage request )
    {
      if ( request.Properties.TryGetValue( HttpPipelineBuilder.HttpContextAccessKey, out var value ) )
        return (HttpContext) value;

      else
        return null;
    }






    /// <summary>
    /// 重写请求的 Host 属性
    /// </summary>
    /// <param name="middleware">上游管线</param>
    /// <param name="host">要重写的主机头</param>
    /// <returns>请求处理管线</returns>
    public static IHttpPipelineMiddleware RewriteHost( this IHttpPipelineMiddleware middleware, string host )
    {
      return middleware.Pipe( new HttpRequestFilter( request =>
      {
        var url = new UriBuilder( request.RequestUri )
        {
          Host = host
        };

        request.RequestUri = url.Uri;
        return request;
      } ) );
    }




    /// <summary>
    /// 将管线接入发出终结点，创建完整的处理管线
    /// </summary>
    /// <param name="middleware">上游管线</param>
    /// <returns>完整的处理管线</returns>
    public static IHttpPipeline Emit( this IHttpPipelineMiddleware middleware )
    {
      return middleware.Pipe( new HttpEmitter() );
    }



    /// <summary>
    /// 使用 HTTP 请求处理管线
    /// </summary>
    /// <param name="application">ASP.NET Core 应用构建器</param>
    /// <param name="configure">处理管线构建程序</param>
    public static void UsePipeline( this IApplicationBuilder application, Action<HttpPipelineBuilder> configure )
    {
      var builder = new HttpPipelineBuilder();
      configure( builder );
      application.Use( builder.Build() );
    }


    /// <summary>
    /// 使用 HTTP 请求处理管线
    /// </summary>
    /// <param name="application">ASP.NET Core 应用构建器</param>
    /// <param name="pipeline">HTTP 请求处理管线</param>
    public static void UsePipeline( this IApplicationBuilder application, IHttpPipeline pipeline )
    {
      var builder = new HttpPipelineBuilder();
      builder.Pipe( pipeline );
      application.Use( builder.Build() );
    }

  }
}