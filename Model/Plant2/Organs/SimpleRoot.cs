using System;
using System.Collections.Generic;
using System.Text;
using CSGeneral;
using ManagerHelpers;

public class SimpleRoot : Organ
   {
   private double[] Uptake = null;

   
   [Event] public event ApsimTypeDelegate WaterChanged;

   public override double DMDemand { get { return 0; } }
   public override double DMSupply { get { return 0; } }
   public override Biomass Live { get { return new Biomass(); } }
   public override Biomass Dead { get { return new Biomass(); } }
   public override double DMRetranslocationSupply { get { return 0; } }
   public override double DMRetranslocation { set { } }
   public override double DMAllocation {set{}}
   public override double WaterDemand { get { return 0; } }
   [Output] [Units("mm")] public double WaterUptake {get { return -MathUtility.Sum(Uptake); }}
   public override double WaterAllocation
      {
      get { return 0; }
      set
         {
         throw new Exception("Cannot set water allocation for roots");
         }
      }


   [Output]public override double WaterSupply
      {
      get
         {
         PaddockType MyPaddock = new PaddockType(Root);

         if (MyPaddock.Soil != null)
            {
            double[] SWSupply = MyPaddock.Component["root"].Variable["SWSupply"].ToDoubleArray();
            return MathUtility.Sum(SWSupply);
            }
         else
            {
            double Total = 0;
            foreach (PaddockType SP in MyPaddock.SubPaddocks)
               {
               double[] SWSupply = SP.Component["root"].Variable["SWSupply"].ToDoubleArray();
               Total += MathUtility.Sum(SWSupply);
               }
            return Total;
            }
         }
      }




   public override void DoWaterUptake(double Amount)
      {
      PaddockType MyPaddock = new PaddockType(Root);
      if (MyPaddock.Soil != null)
         {
         MyPaddock.Component["root"].Variable["SWUptake"].Set(Amount);
         }
      else
         {
         double[] Supply = new double[MyPaddock.SubPaddocks.Count];
         int i=0;
         double Total = 0;
         foreach (PaddockType SP in MyPaddock.SubPaddocks)
            {
            double[] SWSupply = SP.Component["root"].Variable["SWSupply"].ToDoubleArray();
            Supply[i] = (MathUtility.Sum(SWSupply));
            Total += Supply[i];
            i++;
            }
         double fraction = Amount / Total;
         if (fraction > 1)
            throw new Exception("Requested SW uptake > Available supplies.");
         i = 0;
         foreach (PaddockType SP in MyPaddock.SubPaddocks)
            {
            SP.Component["root"].Variable["SWUptake"].Set(Supply[i] * fraction);
            i++;
            }

         }

      }




   }
   
