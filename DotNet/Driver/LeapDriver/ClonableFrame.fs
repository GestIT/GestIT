namespace ClonableLeapFrame

    open Leap
    open System.Collections.Generic
    open System.Runtime.Serialization
    open System.Security.Permissions
    open System.Diagnostics

    ///</member>
    ///<member name="N:ClonableLeapFrame">
    ///  <summary>
    ///  <para>File:   ClonableFrame.fs</para>
    ///  <para>Author: Marta Martino</para>
    ///  <para>ClonableFrame defines a kind of Frame object, with the clonable property in addition.</para>
    ///  </summary>
    type private IgnoreMe () =
      class
      end

    /// <summary>
    /// Representation of ids. Every instance contains a different GUID.
    /// </summary>
    [<AllowNullLiteralAttribute>]
    type FakeId () =
        let mutable id = System.Guid.NewGuid()   // http://it.wikipedia.org/wiki/GUID
//    [<AllowNullLiteralAttribute>]
//    type FakeId () =
//        let mutable id = System.Guid.Empty
//        do
//          id <- System.Guid.NewGuid()   // http://it.wikipedia.org/wiki/GUID
        /// <summary>
        /// Get the id.
        /// </summary>
        member x.ID with get() = id
        /// <summary>
        /// Equals of two ids.
        /// </summary>
        /// <returns>True if the ids are equals. False otherwise.</returns>
        override x.Equals o =
          match o with
          | :? FakeId as oo -> x.ID.Equals(oo.ID)
          | _ -> false
        /// <summary>
        /// Get the hashcode of the id.
        /// </summary>
        /// <returns> Hashcode of GUID.</returns>
        override x.GetHashCode () =
          id.GetHashCode()

    /// <typeparam name="TimeStamp">
    /// Defines a generic type of timestamp.
    /// </typeparam>
    type TimeStamp = int64

    /// <summary>
    /// Defines a Pointable object, which could be a finger or a tool.
    /// </summary>
    [<System.Serializable>]
    type ClonablePointable (id:FakeId, ih:FakeId, d:Vector, p:Vector, v:Vector, isFinger:bool, isTool:bool, length:float32, width:float32) =
        let mutable direction = new Vector(d)
        let mutable position = new Vector(p)
        let mutable velocity = new Vector(v)
        let mutable idHand = ih

        interface ISerializable with
            [<SecurityPermissionAttribute(SecurityAction.Demand,SerializationFormatter=true)>]
            member x.GetObjectData(info:SerializationInfo, context:StreamingContext) =
                info.AddValue("id", id)
                info.AddValue("idHand", idHand)
                info.AddValue("isFinger", isFinger)
                info.AddValue("isTool", isTool)
                info.AddValue("length", length)
                info.AddValue("width", width)
                info.AddValue("d.x", direction.x)
                info.AddValue("d.y", direction.y)
                info.AddValue("d.z", direction.z)
                info.AddValue("p.x", position.x)
                info.AddValue("p.y", position.y)
                info.AddValue("p.z", position.z)
                info.AddValue("v.x", velocity.x)
                info.AddValue("v.y", velocity.y)
                info.AddValue("v.z", velocity.z)
        
        ///</member>
        ///<member name="M:ClonableLeapFrame.ClonablePointable.#ctor(ClonableLeapFrame.FakeId,ClonableLeapFrame.FakeId,Leap.Vector,Leap.Vector,Leap.Vector,System.Boolean,System.Boolean,System.Single,System.Single)">
        /// <summary>
        /// Constructor puppa
        /// </summary>
        /// <param name="id">FakeId.</param>
        /// <param name="ih">FakeId of the hand to which the Pointable belongs to.</param>
        /// <param name="d">A Vector representing direction.</param>
        /// <param name="p">A Vector representing position.</param>
        /// <param name="v">A Vector representing velocity.</param>
        /// <param name="isFinger">A boolean representing if Pointable is a finger.</param>
        /// <param name="isTool">A boolean representing if Pointable is a tool.</param>
        /// <param name="length">Length of Pointable.</param>
        /// <param name="width">Width of Pointable.</param>
        member internal x.IgnoreMe () = ()

        /// <summary>
        /// Constructor for deserialization. (For more information: http://msdn.microsoft.com/en-us/library/ty01x675(v=vs.71).aspx )
        /// </summary>
        /// <param name="info">SerializationInfo object.</param>
        /// <param name="context">StreamingContext object.</param>
        new (info:SerializationInfo, context:StreamingContext) =
            let id = info.GetValue("id", typeof<FakeId>) :?> FakeId
            let idhand = info.GetValue("idHand", typeof<FakeId>)  :?> FakeId
            let isfinger = info.GetBoolean("isFinger")
            let isTool = info.GetBoolean("isTool")
            let length = info.GetSingle("length")
            let width = info.GetSingle("width")
            let direction = new Vector(info.GetSingle("d.x"), info.GetSingle("d.y"), info.GetSingle("d.z"))
            let position = new Vector(info.GetSingle("p.x"), info.GetSingle("p.y"), info.GetSingle("p.z"))
            let velocity = new Vector(info.GetSingle("v.x"), info.GetSingle("v.y"), info.GetSingle("v.z"))
            ClonablePointable(id, idhand, direction, position, velocity, isfinger, isTool, length, width)

        /// <summary>
        /// Get or set direction property.
        /// </summary>
        /// <param name="d">Direction.</param>
        member this.Direction with get() = direction and set(d) = direction <- new Vector(d)
        /// <summary>
        /// Get or set position property.
        /// </summary>
        /// <param name="d">Position.</param>
        member this.Position with get() = position and set(p) = position <- new Vector(p)
        /// <summary>
        /// Get or set velocity property.
        /// </summary>
        /// <param name="d">Velocity.</param>
        member this.Velocity with get() = velocity and set(v) = velocity <- new Vector(v)
        member this.Id with get() = id
        /// <summary>
        /// Get or set the id of the hand to which the pointable belongs to.
        /// </summary>
        /// <param name="d">Hand id.</param>
        member this.IdHand with get() = idHand and set(v) = idHand <- v
        member this.Length = length
        member this.Width = width
        member this.IsFinger = isFinger
        member this.IsTool = isTool
        /// <summary>
        /// Clones the MyPointable object on which the method is called.
        /// </summary>
        /// <returns>New ClonablePointable</returns>
        member this.Clone () =
            new ClonablePointable(this.Id, this.IdHand, this.Direction, this.Position, this.Velocity, this.IsFinger, this.IsTool, this.Length, this.Width)
      
    /// <summary>
    /// Defines a Hand object.
    /// </summary>
    [<System.Serializable>]
    type ClonableHand (i:FakeId, d:Vector, p:Vector, v:Vector, n:Vector, c:Vector, r:float32) =
        let mutable direction = new Vector(d)
        let mutable position = new Vector(p)
        let mutable velocity = new Vector(v)
        let normal = new Vector(n)
        let sphereCenter = new Vector(c)
        let sphereRadius = r
        let id = i

        interface ISerializable with
            [<SecurityPermissionAttribute(SecurityAction.Demand,SerializationFormatter=true)>]
            member x.GetObjectData(info:SerializationInfo, context:StreamingContext) =
                info.AddValue("id", id)
                info.AddValue("sr", sphereRadius)
                info.AddValue("n.x", normal.x)
                info.AddValue("n.y", normal.y)
                info.AddValue("n.z", normal.z)
                info.AddValue("sc.x", sphereCenter.x)
                info.AddValue("sc.y", sphereCenter.y)
                info.AddValue("sc.z", sphereCenter.z)
                info.AddValue("d.x", direction.x)
                info.AddValue("d.y", direction.y)
                info.AddValue("d.z", direction.z)
                info.AddValue("p.x", position.x)
                info.AddValue("p.y", position.y)
                info.AddValue("p.z", position.z)
                info.AddValue("v.x", velocity.x)
                info.AddValue("v.y", velocity.y)
                info.AddValue("v.z", velocity.z)

        ///</member>
        ///<member name="M:ClonableLeapFrame.ClonableHand.#ctor(ClonableLeapFrame.FakeId,Leap.Vector,Leap.Vector,Leap.Vector,Leap.Vector,Leap.Vector,System.Single)">
        /// <summary>
        /// Constructor puppa
        /// </summary>
        /// <param name="i">FakeId.</param>
        /// <param name="d">A Vector representing direction.</param>
        /// <param name="p">A Vector representing position.</param>
        /// <param name="v">A Vector representing velocity.</param>
        /// <param name="n">A Vector perpendicular to the plane formed by the palm of the hand.</param>
        /// <param name="c">A Vector representing the center of the sphere.</param>
        /// <param name="r">The radius of the sphere.</param> 
        member internal x.IgnoreMe () = ()

        /// <summary>
        /// Constructor for deserialization. (For more information: http://msdn.microsoft.com/en-us/library/ty01x675(v=vs.71).aspx )
        /// </summary>
        /// <param name="info">SerializationInfo object.</param>
        /// <param name="context">StreamingContext object.</param>
        new (info:SerializationInfo, context:StreamingContext) =
            let id = info.GetValue("id", typeof<FakeId>) :?> FakeId
            let spRad = info.GetSingle("sr")
            let normal = new Vector(info.GetSingle("n.x"), info.GetSingle("n.y"), info.GetSingle("n.z"))
            let spCen = new Vector(info.GetSingle("sc.x"), info.GetSingle("sc.y"), info.GetSingle("sc.z"))
            let direction = new Vector(info.GetSingle("d.x"), info.GetSingle("d.y"), info.GetSingle("d.z"))
            let position = new Vector(info.GetSingle("p.x"), info.GetSingle("p.y"), info.GetSingle("p.z"))
            let velocity = new Vector(info.GetSingle("v.x"), info.GetSingle("v.y"), info.GetSingle("v.z"))
            ClonableHand(id, direction, position, velocity, normal, spCen, spRad)

        /// <summary>
        /// Get or set direction property.
        /// </summary>
        /// <param name="d">Direction.</param>
        member this.Direction with get() = direction and set(d) = direction <- d
        /// <summary>
        /// Get or set position property.
        /// </summary>
        /// <param name="d">Position.</param>
        member this.Position with get() = position and set(p) = position <- p
        /// <summary>
        /// Get or set velocity property.
        /// </summary>
        /// <param name="d">Velocity.</param>
        member this.Velocity with get() = velocity and set(v) = velocity <- v
        member this.Normal = normal
        /// <summary>
        /// Get the hand id.
        /// </summary>
        member this.Id with get() = id
        member this.SphereCenter = sphereCenter
        member this.SphereRadius = sphereRadius
        /// <summary>
        /// Clones the MyHand object on which the method is called.
        /// </summary>
        /// <returns>New ClonableHand.</returns>
        member this.Clone () =
            new ClonableHand(this.Id, this.Direction, this.Position, this.Velocity, this.Normal, this.SphereCenter, this.SphereRadius)

    /// <summary>
    /// Defines a Frame object. When created, a frame is always invalid.
    /// </summary>
    type ClonableFrame () =
        let pointableList = new Dictionary<FakeId,ClonablePointable>()
        let handList = new Dictionary<FakeId,ClonableHand>()
        let mutable timestamp:TimeStamp = -1L
        /// <summary>
        /// Get the list of Pointable object.
        /// </summary>
        member this.PointableList = pointableList
        /// <summary>
        /// Get the list of Hand object.
        /// </summary>
        member this.HandList = handList
        /// <summary>
        /// Get or set the timestamp of the frame.
        /// </summary>
        member this.Timestamp with get() = timestamp and set(t) = timestamp <- t
        /// <summary>
        /// Get the validity of frame.
        /// <returns>True if it is valid.</returns>
        /// <returns>False otherwise.</returns>
        /// </summary>
        member this.IsValid = timestamp >= 0L
        /// <summary>
        /// Clones the MyFrame object on which the method is called.
        /// <returns>The cloned frame.</returns>
        /// </summary>
        /// <returns>New ClonableFrame.</returns>
        member this.Clone () =
            let f = new ClonableFrame()
            f.Timestamp <- this.Timestamp
            for h in this.HandList do
                f.HandList.Add(h.Key, h.Value.Clone())
            for p in this.PointableList do
                f.PointableList.Add(p.Key, p.Value.Clone())
            f
