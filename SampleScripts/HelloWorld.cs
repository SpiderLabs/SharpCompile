using System;
namespace HelloWorld
{
    class Hello
    {
        static void Main(string[] args)
        {
            string hello = "Hello World!";
	    if(args.Length == 1)
	        hello = string.Format("Hello {0}!", args[0]);

            Console.WriteLine(hello);
        }
    }
}
