#region === Copyright (c) 2010 Pascal van der Heiden ===

using System;
using System.Collections.Generic;
using System.Threading;
using CodeImp.DoomBuilder.Plugins.VisplaneExplorer.Vpo;

#endregion

namespace CodeImp.DoomBuilder.Plugins.VisplaneExplorer
{
	internal class VPOManager : IDisposable
	{
		#region ================== Constants

		public const int POINTS_PER_ITERATION = 100;
		private const int EXPECTED_RESULTS_BUFFER = 200000;

		private readonly int[] TEST_ANGLES = new[] { 0, 90, 180, 270, 45, 135, 225, 315 /*, 22, 67, 112, 157, 202, 247, 292, 337 */ };
		
		#endregion

		#region ================== Variables

		// Main objects
		private List<Thread> threads = new List<Thread>();

		// Map to load
		private string filename;
		private string mapname;

		// Input and output queue and stop flag (all require a lock on 'points' !)
		private readonly Queue<TilePoint> points = new Queue<TilePoint>(EXPECTED_RESULTS_BUFFER);
		private readonly Queue<PointData> results = new Queue<PointData>(EXPECTED_RESULTS_BUFFER);
		private bool stopflag;
		
		#endregion

		#region ================== Properties

		// Use up to 75% of CPU cores available
		public int NumThreads { get { return Math.Max((Environment.ProcessorCount * 3 + 2) / 4, 1); } }

		#endregion

		#region ================== Constructor / Destructor
		
		// Constructor
		public VPOManager()
		{
		}

		// Disposer
		public void Dispose()
		{
			Stop();
		}
		
		#endregion

		#region ================== Processing

		// The thread!
		private void ProcessingThread()
		{
			VpoContext context = new VpoContext();

			// Load the map
			bool isHexen = General.Map.HEXEN;
			if(!context.LoadWad(filename)) throw new Exception("VPO is unable to read this file:" + (context.Error ?? "<unknown error>"));
			if(!context.OpenMap(mapname, ref isHexen)) throw new Exception("VPO is unable to open this map:" + (context.Error ?? "<unknown error>"));
			context.OpenDoorSectors(BuilderPlug.InterfaceForm.OpenDoors ? 1 : -1); //mxd

			// Processing
			Queue<TilePoint> todo = new Queue<TilePoint>(POINTS_PER_ITERATION);
			Queue<PointData> done = new Queue<PointData>(POINTS_PER_ITERATION);
			while(true)
			{
				lock(points)
				{
					// Wait for work
					if (points.Count == 0 && !stopflag)
						Monitor.Wait(points);

					// Flush done points to the results
					int numdone = done.Count;
					for(int i = 0; i < numdone; i++)
						results.Enqueue(done.Dequeue());

					if (stopflag)
						break;

					// Get points from the waiting queue into my todo queue for processing
					int numtodo = Math.Min(POINTS_PER_ITERATION, points.Count);
					for(int i = 0; i < numtodo; i++)
						todo.Enqueue(points.Dequeue());
				}
					
				// Process the points
				while(todo.Count > 0)
				{
					TilePoint p = todo.Dequeue();
					PointData pd = new PointData();
					pd.point = p;

					for(int i = 0; i < TEST_ANGLES.Length; i++)
					{
						pd.result = (PointResult)context.TestSpot(p.x, p.y, BuilderPlug.InterfaceForm.ViewHeight, TEST_ANGLES[i],
							ref pd.visplanes, ref pd.drawsegs, ref pd.openings, ref pd.solidsegs);
					}

					done.Enqueue(pd);
				}
			}

			context.FreeWad();
		}

		#endregion

		#region ================== Public Methods

		// This loads a map
		public void Start(string filename, string mapname)
		{
			Stop();

			this.filename = filename;
			this.mapname = mapname;

			// Start a thread on each core
			for(int i = 0; i < NumThreads; i++)
			{
				var thread = new Thread(ProcessingThread);
				thread.Name = "Visplane Explorer " + i;
				thread.Start();
				threads.Add(thread);
			}
		}

		// This frees the map
		public void Stop()
		{
			lock (points)
			{
				stopflag = true;
				Monitor.PulseAll(points);
			}

			foreach (Thread thread in threads)
			{
				thread.Join();
			}
			threads.Clear();

			lock (points)
			{
				results.Clear();
				points.Clear();
				stopflag = false;
			}
		}

		// This gives points to process and returns the total points left in the buffer
		public int EnqueuePoints(IEnumerable<TilePoint> newpoints)
		{
			lock(points)
			{
				foreach(TilePoint p in newpoints)
					points.Enqueue(p);
				Monitor.PulseAll(points);
				return points.Count;
			}
		}

		// This fetches results (in 'data') and returns the number of points
		// remaining to be processed.
		public int DequeueResults(List<PointData> data)
		{
			lock(points)
			{
				int numresults = results.Count;
				if(data.Capacity - data.Count < numresults)
					data.Capacity = data.Count + numresults;
				for(int i = 0; i < numresults; i++)
					data.Add(results.Dequeue());
				return points.Count;
			}
		}

		// This returns the number of points left in the buffer
		public int GetRemainingPoints()
		{
			lock(points)
			{
				return points.Count;
			}
		}

		#endregion
	}
}
