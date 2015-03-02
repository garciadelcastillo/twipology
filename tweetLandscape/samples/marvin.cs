using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;

using GH_IO;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;


using MARVIN;



/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
#region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { /* Implementation hidden. */ }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { /* Implementation hidden. */ }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { /* Implementation hidden. */ }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
#endregion

#region Members
  /// <summary>Gets the current Rhino document.</summary>
  private readonly RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private readonly GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private readonly IGH_Component Component;
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private readonly int Iteration;
#endregion

  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments,
  /// Output parameters as ref arguments. You don't have to assign output parameters,
  /// they will have a default value.
  /// </summary>
  private void RunScript(System.Object Robot, System.Object Tool, DataTree<Plane> TargetFrames, ref object Q, ref object EndEffectorFrames, ref object ToolFrames, ref object JointFrames, ref object Skeletons, ref object _____debug_____, ref object QString, ref object AllSolutions)
  {

    // Optional component parameters
    bool robotAttached = Component.Params.Input.ElementAt(Component.Params.IndexOfInputParam("Robot")).SourceCount != 0;
    bool toolAttached = Component.Params.Input.ElementAt(Component.Params.IndexOfInputParam("Tool")).SourceCount != 0;
    bool targetFramesAttached = Component.Params.Input.ElementAt(Component.Params.IndexOfInputParam("TargetFrames")).SourceCount != 0;

    // Attachments definition and sanity
    // Robot
    if (!robotAttached)
    {
      throw new System.InvalidOperationException("Please provide a Robot definition object");
    }
    Robot r = (Robot) Robot;  // cast it to an assembly object

    // Tool
    Tool tool = null;
    if (toolAttached) {
      tool = (Tool) Tool;  // cast it to an assembly object
    }
    else
    {
      tool = new Tool("no_tool", Plane.WorldXY, Plane.WorldXY, 1);
    }

    // Frames
    if (!targetFramesAttached)
    {
      throw new System.InvalidOperationException("Please provide at least one Target Frame");
    }
    DataTree <Plane> flatTargets = TargetFrames;
    flatTargets.Flatten();
    List<Plane> targets = flatTargets.Branches[0];
    int targetCount = targets.Count;
    if (targets.Count == 0)
    {
      throw new System.InvalidOperationException("Please provide at least one Target Frame");
    }


    // preallocate outputs
    DataTree<double> rotations = new DataTree<double>();
    DataTree<Plane> endEffectorFrames = new DataTree<Plane>();
    DataTree<Plane> toolFrames = new DataTree<Plane>();
    DataTree<Plane> jointFrames = new DataTree<Plane>();
    DataTree<Polyline> skeletons = new DataTree<Polyline>();


    // compute inverse kinematics
    List<Solution> solutions = new List<Solution>();
    if (toolAttached)
      solutions = r.inverseKinematicsToolpath(targets, tool);
    else
      solutions = r.inverseKinematicsToolpath(targets);


    // DEBUG
    DataTree<Solution> allSols = new DataTree<Solution>();
    int ccc = 0;
    foreach(Plane p in targets)
    {
      GH_Path path = new GH_Path(ccc++);
      allSols.AddRange(r.inverseKinematics(p, tool), path);
    }

    AllSolutions = allSols;


    for (int i = 0; i < targetCount; i++)
    {
      GH_Path path = new GH_Path(i);

      if (solutions[i].isValid)
      {
        rotations.AddRange(solutions[i].rotations, path);

        List<Plane> jFrames = r.forwardKinematics(solutions[i].rotations, true);
        jointFrames.AddRange(jFrames, path);

        Plane eef = jFrames.Last();
        endEffectorFrames.Add(eef, path);
        Plane tf = eef;
        if (toolAttached) {
          Plane toolFrameW = tool.toolFrame;
          Transform toolbaseToEEF = Transform.PlaneToPlane(tool.baseFrame, eef);
          toolFrameW.Transform(toolbaseToEEF);
          tf = toolFrameW;
        }
        toolFrames.Add(tf, path);

        // skeleton
        List<Point3d> jPoints = new List<Point3d>();
        foreach(Plane p in jFrames) {
          jPoints.Add(p.Origin);
        }
        if (toolAttached) {
          jPoints.Add(tf.Origin);
        }

        skeletons.Add(new Polyline(jPoints), path);
      }

      else {
        // don't add anything
      }
    }


    Q = rotations;
    EndEffectorFrames = endEffectorFrames;
    ToolFrames = toolFrames;
    JointFrames = jointFrames;
    Skeletons = skeletons;

    QString = solutions;

  }

  // <Custom additional code> 

  // </Custom additional code> 
}