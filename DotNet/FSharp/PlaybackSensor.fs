/// <summary>
/// File:   PlaybackSensor.fs
/// Author: Marta Martino
/// PlaybackSensor receives as input a stream. This file has be written by LeapDriver with the information about device's event.
/// It permits to simulate the Petri's Net behaviour without the direct usage of Leap sensor, triggering events assembled from the input file.
/// </summary>
namespace GestIT

    open System.IO
    open System.Runtime.Serialization.Formatters.Binary
    open System.Threading

    /// </member>
    /// <member name="N:LeapDriver">
    /// <summary>
    /// <para>File:   PlaybackSensor.fs</para>
    /// <para>Author: Marta Martino</para>
    /// <para>PlaybackSensor receives as input a stream. This file has be written by LeapDriver with the information about device's event.
    /// It permits to simulate the Petri's Net behaviour without the direct usage of Leap sensor, triggering events assembled from the input file.</para>
    /// </summary>
    type private IgnoreMe () =
      class
      end

    /// <summary>
    /// It represent PlaybackSensor.
    /// </summary>
    /// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
    /// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
    type PlaybackSensor<'T, 'U> when 'T :> System.Enum and 'U :> System.EventArgs (s:Stream) =
      let evt = new Event<SensorEventArgs<'T,'U>>()
      let ser = new BinaryFormatter()

      /// <summary>
      /// Deserialize stream into a tuple of three elements.
      /// </summary>
      let readObj () =
        ser.Deserialize(s) :?> (System.DateTime*'T*'U)

      /// </member>
      /// <member name="M:GestIT.PlaybackSensor`2.#ctor(System.IO.Stream)">
      /// <summary>
      /// Assigns the stream to be read.
      /// </summary>
      /// <param name="s"> The stream from which the sensor read information to send. </param>
      member private x.IgnoreMe() = ()

      /// <summary>
      /// In starts a worker thread, which works on deserializing data and triggering events.
      /// </summary>
      member this.start () =
        let worker = new Thread(fun () -> 
          let (t, typ, ev) = readObj()
          let mutable lastTime = t
          evt.Trigger(new SensorEventArgs<'T, 'U>(typ, ev))

          let mutable eof = false
          while not eof do
            try
              let (t, typ, ev) = readObj()
              let dt = (t - lastTime).TotalMilliseconds
              lastTime <- t
              if  dt > 5. then Thread.Sleep(int dt)
              evt.Trigger(new SensorEventArgs<'T, 'U>(typ, ev))
            with _ -> eof <- true
        )
        worker.IsBackground <- true
        worker.Start()

      interface ISensor<'T,'U> with
        [<CLIEvent>]
        member x.SensorEvents = evt.Publish