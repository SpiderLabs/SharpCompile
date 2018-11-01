using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Handlers;
using uhttpsharp.Handlers.Compression;
using uhttpsharp.Listeners;
using uhttpsharp.ModelBinders;
using uhttpsharp.RequestProviders;
using uhttpsharp.Headers;


namespace SharpCompileServer
{
    public class TimingHandler : IHttpRequestHandler
    {

        public async Task Handle(IHttpContext context, Func<Task> next)
        {
            var stopWatch = Stopwatch.StartNew();
            await next();
        }
    }

    public class IndexHandler : IHttpRequestHandler
    {
        private readonly HttpResponse _response;
        private readonly HttpResponse _keepAliveResponse;
        private string _compilerPath = "";

        public IndexHandler(string compilerPath)
        {
            _compilerPath = compilerPath;
        }

        public Task Handle(IHttpContext context, Func<Task> next)
        {
            byte[] executable = sharpCompileHandler(context);
            HttpResponse executableResponse = new HttpResponse(HttpResponseCode.Ok, executable, false);
            context.Response = executableResponse;
            return Task.Factory.GetCompleted();
        }

        public byte[] sharpCompileHandler(IHttpContext context)
        {
            byte[] outputBytes = new byte[] { 0x00 };
            if(context.Request.Method == HttpMethods.Post)
            {
                try
                {
                    var input = context.Request.Post.Raw;
                    Guid id = Guid.NewGuid();
                    string tempFile = String.Format("{0}.cs", id.ToString());
                    System.IO.File.WriteAllBytes(tempFile, input);
                    Process process = new Process();
                    process.StartInfo.FileName = _compilerPath;
                    process.StartInfo.Arguments = tempFile;
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;
                    process.Start();
                    process.WaitForExit();
                    string executableFile = String.Format("{0}.exe", id.ToString());
                    outputBytes = System.IO.File.ReadAllBytes(executableFile);
                    System.IO.File.Delete(executableFile);
                    System.IO.File.Delete(tempFile);
                }
                catch { }
            }
            return outputBytes;
        }
    }

    public class ErrorHandler : IHttpRequestHandler
    {
        public Task Handle(IHttpContext context, System.Func<Task> next)
        {
            context.Response = new HttpResponse(HttpResponseCode.NotFound, "These are not the droids you are looking for.", true);
            return Task.Factory.GetCompleted();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Test Invoke-RestMethod -Uri http://127.0.0.1:8080/ -Method Post -InFile .\test.cs -OutFile .\foo.exe

            string compilerPath = "C:\\Windows\\Microsoft.NET\\Framework\\v2.0.50727\\csc.exe";
            if (args.Count() > 0)
            {
                compilerPath = args[0];
            }
            using (var httpServer = new HttpServer(new HttpRequestProvider()))
            {
                if (System.IO.File.Exists(@"cert.cer"))
                {
                    int port = 443;
                    if (args.Count() > 1)
                    {
                        port = int.Parse(args[1]);
                    }
                    var serverCertificate = X509Certificate.CreateFromCertFile(@"cert.cer");
                    httpServer.Use(new ListenerSslDecorator(new TcpListenerAdapter(new TcpListener(IPAddress.Any, port)), serverCertificate));
                }
                else
                {
                    int port = 80;
                    if (args.Count() > 1)
                    {
                        port = int.Parse(args[1]);
                    }
                    httpServer.Use(new TcpListenerAdapter(new TcpListener(IPAddress.Any, port)));
                }
                // Request handling : 
                httpServer.Use((context, next) => {
                    Console.WriteLine("Got Request!");
                    return next();
                });

                // Handler classes : 
                httpServer.Use(new TimingHandler());
                httpServer.Use(new HttpRouter().With(string.Empty, new IndexHandler(compilerPath))
                                               .With("about", new IndexHandler(compilerPath)));

                httpServer.Use(new FileHandler());
                httpServer.Use(new ErrorHandler());

                httpServer.Start();

                Console.ReadLine();
            }

        }
    }
}
