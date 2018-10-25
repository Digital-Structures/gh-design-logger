using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

using System.IO;
using System.Windows.Forms;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace DesignLogger
{
    public class DesignLoggerComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>

        /// <summary>

        /// Log file.

        /// </summary>

        protected StreamWriter file = null;

        /// Previous month value.
        protected int oldMonth = -1;

        /// Previous day of month value.
        protected int oldDay = -1;

        /// Previous hour of day value.
        protected double oldHour = -1.0;

        public DesignLoggerComponent()
          : base("DesignLogger", "DLogger",
              "Logs a design exploration session",
              "DSE", "Catalog")
        {

            this.DVecs = new List<List<double>>();
            this.OVecs = new List<List<double>>();
            this.DVec = new List<double>();
            this.OVec = new List<double>();
            this.Favorites = new List<List<double>>();
            this.timelist = new List<string>();
            this.sectionlist = new List<int>();
            this.elapsedlist = new List<double>();
            this.count = new int();
            this.favCount = new List<double>();
            this.timeListDate = new List<DateTime>();
        }

        public List<List<double>> DVecs;
        public List<List<double>> OVecs;
        public List<List<double>> Favorites;
        public List<double> DVec;
        public List<double> OVec;
        public List<string> timelist;
        public List<DateTime> timeListDate;
        public List<double> elapsedlist;
        public List<int> sectionlist;
        public Boolean favorite = false;
        public Boolean End = false;
        public string path;
        public int participant;
        public Boolean first = true;
        public int count;
        public List<double> favCount;
        public double lastTime = 0;
        public int section;
        System.Timers.Timer sectionTimer;
        System.Timers.Timer saveTimer;


        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            pManager.AddBooleanParameter("Start", "S", "Start the session", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Participant Number", "PN", "Participant ID number", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Session Length", "SL", "Session length in minutes", GH_ParamAccess.item, 0.5);
            pManager.AddTextParameter("Log Path", "L", "Log file path", GH_ParamAccess.item, (string)null);

            pManager.AddIntegerParameter("Phase", "Phase", "Design study phase: 1-4", GH_ParamAccess.item, 1);

            pManager.AddNumberParameter("Design Vector", "DVec", "Reads design vector", GH_ParamAccess.list);
            pManager.AddNumberParameter("Objective Vector", "OVec", "Reads objective vector", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Favorite", "Fav", "Favorite", GH_ParamAccess.item, false);

            pManager.AddBooleanParameter("End", "E", "End the session", GH_ParamAccess.item, false);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.AddNumberParameter("Favorites", "Favs", "List of 'Favorite' designs", GH_ParamAccess.list);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            if (file == null)
            {

                // See if we are started the component
                bool running = false;
                DA.GetData(0, ref running);
                if (!running) return;


                if (first == true)
                {

                    DA.GetData(1, ref participant);

                    double minutes = 30.0;
                    DA.GetData(2, ref minutes);
                    if (!(minutes > 0.0)) return;

                    DA.GetData(3, ref path);


                    DialogResult result = MessageBox.Show(string.Format("Your {0}-minute session will begin when you press OK.", minutes), "Start Session", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                    if (result != DialogResult.OK) return;

                    // Set up section timer (COMMENTED--DO TIMING MANUALLY)
                    //sectionTimer = new System.Timers.Timer();
                    //sectionTimer.Elapsed += new System.Timers.ElapsedEventHandler(nextSection);
                    //sectionTimer.Interval = 15 * 60 * 1000;
                    //sectionTimer.Enabled = true;

                    // Set up autosave timer
                    saveTimer = new System.Timers.Timer();
                    saveTimer.Elapsed += new System.Timers.ElapsedEventHandler(autoSaveProgress);
                    // Autosave every 10 minutes
                    saveTimer.Interval = 1 * 60 * 1000;
                    saveTimer.Enabled = true;
                    section = 1;

                    first = false;

                    // Populate initial lists
                    favCount.Add(0);
                    timelist.Add("0");
                    sectionlist.Add(0);
                    elapsedlist.Add(0);
                    timeListDate.Add(DateTime.Now);
                    List<double> DVecStart = new List<double>();
                    List<double> OVecStart = new List<double>();

                    DA.GetDataList<double>(5, DVecStart);
                    DA.GetDataList<double>(6, OVecStart);
       
                    DVecs.Add(DVecStart);
                    OVecs.Add(OVecStart);


                }
            }


            DVec.Clear();
            OVec.Clear();

            List<double> DVecTemp = new List<double>();
            List<double> OVecTemp = new List<double>();

            DA.GetData(4, ref section);
            DA.GetDataList<double>(5, DVecTemp);
            DA.GetDataList<double>(6, OVecTemp);

            DA.GetData(7, ref favorite);
            DA.GetData(8, ref End);


            // Give threshold for recording data
            //double currentTime = DateTime.Now.Ticks;

            string now = DateTime.Now.ToString("yyyyMMddHHmmss");
            double nowTime = Convert.ToDouble(now);
            double lastTime = Convert.ToDouble(timelist[count]);
            double timeDif = DateTime.Now.Subtract(timeListDate[count]).TotalSeconds;

            if (nowTime - lastTime > .0001)
            {

                DVecs.Add(DVecTemp);
                OVecs.Add(OVecTemp);
                timelist.Add(DateTime.Now.ToString("yyyyMMddHHmmss"));
                timeListDate.Add(DateTime.Now);
                elapsedlist.Add(timeDif);
                sectionlist.Add(section);

                count++;
            }

                if (favorite == true)
                {
                    Favorites.Add(DVec);
                }


                if (End == true)
                {
                    PrintAllSolutions();
                    //sectionTimer.Stop();
                    saveTimer.Stop();
                    endSession();

                }

                // keep counter and mark favorites

                favCount.Add(0);
                
                if (favorite == true)
                {
                    MakeFavorite(DVecTemp);
                }

                //Add favorites output

                DA.SetDataTree(0, ListOfListsToTree<double>(this.Favorites));

                //lastTime = DateTime.Now.Ticks;
            
        }

        public void PrintAllSolutions()
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(this.path + participant.ToString() + "_" + DateTime.Now.ToString("HHmmss") + ".csv");

            for (int i = 0; i < this.DVecs.Count; i++)
            {
                string design = "";

                List<double> currentDesign = DVecs[i];
                for (int j = 0; j < currentDesign.Count; j++)
                {
                    design = design + currentDesign[j] + ",";
                }

                List<double> currentObj = OVecs[i];
                for (int j = 0; j < currentObj.Count; j++)
                {
                    design = design + currentObj[j] + ",";
                }

                design = design + timelist[i] + ",";
                design = design + elapsedlist[i] + ",";
                design = design + favCount[i] + ",";
                design = design + sectionlist[i];

                file.WriteLine(design);
            }
            file.Close();
        }


        public void endSession()

        {

            //Command.UndoRedo -= HandleUndoRedo;

            MessageBox.Show("Please complete the survey", "Time's Up!", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            System.Diagnostics.Process.Start("https://goo.gl/forms/JaaCTzpEnsi6MTNL2");

        }

        //NOT CURRENTLY USED--can be used to automate timing and section
        //public void nextSection(object sender, EventArgs e)
        //{
        //    MessageBox.Show("Section time up! Please move on to next section.  If all four are completed, please press 'end session'", "Next Section", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        //    section = section + 1;

        //    if (section > 4)
        //    {
        //        sectionTimer.Stop();
        //        saveTimer.Stop();
        //    }

        //}


        public void MakeFavorite(List<double> Temp)
        {
                favCount[count] = 1;
                Favorites.Add(Temp);            
        }



    protected void log(string message)
        {
            if (file != null)
                file.WriteLine("{0}: {1}", DateTime.Now.ToString(), message);
        }

        static DataTree<T> ListOfListsToTree<T>(List<List<T>> listofLists)
        {
            DataTree<T> tree = new DataTree<T>();
            for (int i = 0; i < listofLists.Count; i++)
            {
                tree.AddRange(listofLists[i], new GH_Path(i));
            }
            return tree;
        }


        private void autoSaveProgress(object sender, EventArgs e)
        {

            System.IO.StreamWriter file = new System.IO.StreamWriter(this.path + participant.ToString() + "_" +"Backup" + ".csv");

            for (int i = 0; i < this.DVecs.Count; i++)
            {
                string design = "";

                List<double> currentDesign = DVecs[i];
                for (int j = 0; j < currentDesign.Count; j++)
                {
                    design = design + currentDesign[j] + ",";
                }

                List<double> currentObj = OVecs[i];
                for (int j = 0; j < currentObj.Count; j++)
                {
                    design = design + currentObj[j] + ",";
                }

                design = design + timelist[i] + ",";
                design = design + elapsedlist[i] + ",";
                design = design + favCount[i] + ",";
                design = design + sectionlist[i];

                file.WriteLine(design);
            }
            file.Close();

        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return DesignLogger.Properties.Resources.Logger;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8edb7005-f3b2-4a1e-aa48-b84c2df3e204"); }
        }
    }
}
