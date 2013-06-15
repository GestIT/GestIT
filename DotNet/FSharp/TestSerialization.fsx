#r @"C:\Users\Pc\Desktop\Leap_Developer_Kit_0.7.9_3460_Windows\LeapSDK\lib\LeapCSharp.NET3.5.dll"
#load "GestIT_Library.fs"
#load "..\\LeapDriver\\ClonableFrame.fs"
#load "..\\LeapDriver\\Driver.fs"

open System.IO
open System.Runtime.Serialization.Formatters.Binary

let outf = @"C:\Users\Pc\Desktop\output.ser"
let f = File.OpenWrite(outf)

let formatter = new BinaryFormatter()

let fakeid = ClonableLeapFrame.FakeId()
let dict = new System.Collections.Generic.Dictionary<ClonableLeapFrame.FakeId, ClonableLeapFrame.ClonableHand>()
dict.[fakeid] <- new ClonableLeapFrame.ClonableHand(fakeid, new Leap.Vector(1.f,2.f,3.f), new Leap.Vector(1.f,2.f,3.f), new Leap.Vector(1.f,2.f,3.f), new Leap.Vector(1.f,2.f,3.f), new Leap.Vector(1.f,2.f,3.f), 0.4f)
dict.[fakeid]

formatter.Serialize(f, (System.DateTime.Now, LeapDriver.LeapFeatureTypes.ActiveFinger, new LeapDriver.LeapEventArgs(new ClonableLeapFrame.ClonableFrame(), fakeid)))
formatter.Serialize(f, (System.DateTime.Now, LeapDriver.LeapFeatureTypes.ActiveHand, new LeapDriver.LeapEventArgs(new ClonableLeapFrame.ClonableFrame(), ClonableLeapFrame.FakeId())))

f.Flush()

f.Close()

let ff = File.OpenRead(outf)

let frm = new BinaryFormatter()

let d = frm.Deserialize(ff) :?> System.DateTime*LeapDriver.LeapFeatureTypes*LeapDriver.LeapEventArgs

ff.Length = ff.Position

ff.Close()

match d with
| a,b,c -> c.Id.Equals(fakeid)

let fanculo =
  match d with
  | a,b,c -> c.Id

fanculo.Equals(fakeid)
fanculo = fakeid

dict.ContainsKey(fakeid)
dict.ContainsKey(fanculo)

fakeid.GetHashCode()
fanculo.GetHashCode()

//---------------------------

fakeid

net

net.Stream <- f

...

net.Stream <- null
