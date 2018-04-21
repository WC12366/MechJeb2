
using System;
using System.Diagnostics;
using UnityEngine;

namespace MuMech {
    public class Pontryagin {
        public struct Engine
        {
            public double mdot, thrust;
            public Engine(double mdot, double thrust)
            {
                this.mdot = mdot;
                this.thrust = thrust;
            }
        }

        public struct Problem
        {
            public Vector3d r0;
            public Vector3d v0;
            public double m0;
            public Vector3d rT;
            public Vector3d vT;

            public Problem(Vector3d r0, Vector3d v0, double m0, Vector3d rT, Vector3d vT)
            {
                this.r0 = r0;
                this.v0 = v0;
                this.m0 = m0;
                this.rT = rT;
                this.vT = vT;
            }
        }

        public Pontryagin()
        {
        }

        public static void centralForceThrust(double[] y, double x, double[] dy, object o)
        {
            Engine e = (Engine)o;
            double Fm = e.thrust / y[12];
            double r2 = y[0]*y[0] + y[1]*y[1] + y[2]*y[2];
            double r = Math.Sqrt(r2);
            double r3 = r2 * r;
            double r5 = r3 * r2;
            double pvm = Math.Sqrt(y[6]*y[6] + y[7]*y[7] + y[8]*y[8]);
            double rdotpv = y[0] * y[6] + y[1] * y[7] + y[2] * y[8];

            /* dr = v */
            dy[0] = y[3];
            dy[1] = y[4];
            dy[2] = y[5];
            /* dv = - r / r^3 + Fm * u */
            dy[3] = - y[0] / r3 + Fm * y[6] / pvm;
            dy[4] = - y[1] / r3 + Fm * y[7] / pvm;
            dy[5] = - y[2] / r3 + Fm * y[8] / pvm;
            /* dpv = - pr */
            dy[6] = - y[9];
            dy[7] = - y[10];
            dy[8] = - y[11];
            /* dpr = pv / r3 - 3 / r5 dot(r, pv) r */
            dy[9]  = y[6] / r3 - 3 / r5 * rdotpv * y[0];
            dy[10] = y[7] / r3 - 3 / r5 * rdotpv * y[1];
            dy[11] = y[8] / r3 - 3 / r5 * rdotpv * y[2];
            /* m = mdot */
            dy[12] = - e.mdot;
            /* FIXME: should throw if we get down to less than a kg or something */
        }

        // free attachment into the orbit defined by rT + vT
        public static void terminal5constraint(double[] yT, double[] z, Vector3d rT, Vector3d vT)
        {
            Vector3d rf = new Vector3d(yT[0], yT[1], yT[2]);
            Vector3d vf = new Vector3d(yT[3], yT[4], yT[5]);
            Vector3d pvf = new Vector3d(yT[6], yT[7], yT[8]);
            Vector3d prf = new Vector3d(yT[9], yT[10], yT[11]);

            Vector3d hT = Vector3d.Cross(rT, vT);

            if (hT[2] == 0)
            {
                /* degenerate inc=90 case so swap all the coordinates */
                rf = rf.Reorder(231);
                vf = vf.Reorder(231);
                rT = rT.Reorder(231);
                vT = vT.Reorder(231);
                prf = prf.Reorder(231);
                pvf = pvf.Reorder(231);
                hT = Vector3d.Cross(rT, vT);
            }

            Vector3d hf = Vector3d.Cross(rf, vf);
            Vector3d eT = - ( rT.normalized + Vector3d.Cross(hT, vT) );
            Vector3d ef = - ( rf.normalized + Vector3d.Cross(hf, vf) );
            Vector3d hmiss = hf - hT;
            Vector3d emiss = ef - eT;
            double trans = Vector3d.Dot(prf, vf) - Vector3d.Dot(pvf, rf) / ( rf.magnitude * rf.magnitude * rf.magnitude );

            z[0] = hmiss[0];
            z[1] = hmiss[1];
            z[2] = hmiss[2];
            z[3] = emiss[0];
            z[4] = emiss[1];
            z[5] = trans;
        }

        public void singleIntegrate(double[] y0, double[] yf, int n, ref double t, double dt, Engine e)
        {
            double eps = 0.00001;
            double h = 0;
            double[] y = new double[13];
            double[] x = new double[2]{t, t+dt};
            double[] xtbl;
            double[,] ytbl;
            int m;
            alglib.odesolverstate s;
            alglib.odesolverreport rep;
            Array.Copy(y0, 13*n, y, 0, 13);
            alglib.odesolverrkck(y, 13, x, 2, eps, h, out s);
            alglib.odesolversolve(s, centralForceThrust, e);
            alglib.odesolverresults(s, out m, out xtbl, out ytbl, out rep);
            for(int i = 0; i < ytbl.GetLength(1); i++)
            {
                int j = 13*n+i;
                yf[j] = ytbl[1,i];
            }
            t = t + dt;
        }

        public void multipleIntegrate(double[] y0, double[] yf, Problem p)
        {
            double tc = y0[26];
            double tb = y0[27];
            double t = 0;

            Engine e = new Engine(0, 0);
            singleIntegrate(y0, yf, 0, ref t, tc, e);

            Console.WriteLine(t);

            e = new Engine(63136.1585987428, 25092.0703945434);
            singleIntegrate(y0, yf, 1, ref t, tb, e);
            Console.WriteLine(t);
        }

        private void calculateZeros(double[] y0, double[] z, object o)
        {
            Problem p = (Problem)o;

            double[] yf = new double[26];
            multipleIntegrate(y0, yf, p);

            /* initial conditions */
            z[0] = y0[0] - p.r0[0];
            z[1] = y0[1] - p.r0[1];
            z[2] = y0[2] - p.r0[2];
            z[3] = y0[3] - p.v0[0];
            z[4] = y0[4] - p.v0[1];
            z[5] = y0[5] - p.v0[2];
            z[6] = y0[12] - p.m0;

            /* terminal constraints */
            double[] yT = new double[13];
            Array.Copy(yf, 13, yT, 0, 13);
            double[] zterm = new double[6];
            //terminal5constraint(yT, zterm, p);

            z[7] = zterm[0];
            z[8] = zterm[1];
            z[9] = zterm[2];
            z[10] = zterm[3];
            z[11] = zterm[4];
            z[12] = zterm[5];
        }
    }
}