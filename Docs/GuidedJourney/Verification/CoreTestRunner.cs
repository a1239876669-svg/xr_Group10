using System;
using System.Reflection;
using NUnit.Framework;
class Runner
{
 static int Main()
 {
  int pass = 0, fail = 0;
  var type = typeof(Group10.GuidedJourney.Tests.JourneyStateMachineTests);
  foreach (var method in type.GetMethods())
  {
   if (method.GetCustomAttributes(typeof(TestAttribute), true).Length == 0) continue;
   try { method.Invoke(Activator.CreateInstance(type), null); Console.WriteLine("PASS " + method.Name); pass++; }
   catch (Exception e) { Console.WriteLine("FAIL " + method.Name + "\n" + (e.InnerException ?? e)); fail++; }
  }
  Console.WriteLine("RESULT pass=" + pass + " fail=" + fail);
  return pass == 47 && fail == 0 ? 0 : 1;
 }
}