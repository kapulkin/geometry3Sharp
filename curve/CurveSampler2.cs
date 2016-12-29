﻿using System;
using System.Collections.Generic;

namespace g3 {
	
	public static class CurveSampler2 
	{
		public static VectorArray2d AutoSample(IParametricCurve2d curve, double fSpacingLength, double fSpacingT)
		{
			if ( curve is ParametricCurveSequence2 )
				return AutoSample(curve as ParametricCurveSequence2, fSpacingLength, fSpacingT);

			if ( curve.HasArcLength ) {
				if ( curve is NURBSCurve2 )
					return SampleNURBSHybrid(curve as NURBSCurve2, fSpacingLength);
				else
					return SampleArcLen(curve, fSpacingLength);
			} else {
				return SampleT(curve, fSpacingT);
			}
		}



		public static VectorArray2d SampleT(IParametricCurve2d curve, double fSpacing)
		{
			double fLenT = 1.0f;		// assumption for now is that all curves span [0,1] t-range

			int nSteps = Math.Max( (int)(fLenT / fSpacing)+1, 2 );

			VectorArray2d vec = new VectorArray2d(nSteps);

			for ( int i = 0; i < nSteps; ++i ) {
				double t = (double)i / (double)(nSteps-1);
				vec[i] = curve.SampleT(t * fLenT);
			}

			return vec;
		}


		public static VectorArray2d SampleArcLen(IParametricCurve2d curve, double fSpacing) 
		{
			if ( curve.HasArcLength == false )
				throw new InvalidOperationException("CurveSampler2.SampleArcLen: curve does not support arc length sampling!");

			double fLen = curve.ArcLength;
			int nSteps = Math.Max( (int)(fLen / fSpacing)+1, 2 );

			VectorArray2d vec = new VectorArray2d(nSteps);

			for ( int i = 0; i < nSteps; ++i ) {
				double t = (double)i / (double)(nSteps-1);
				vec[i] = curve.SampleArcLength(t * fLen);
			}

			return vec;
		}


		// special case nurbs sampler. Computes a separate sampling of each unique knot interval
		// of the curve parameter space. Reasoning:
		//   1) computing Arc Length of an entire nurbs curve is quite slow if the curve has
		//      repeated knots. these become discontinuities which mean the numerical integrator
		//      has to do a lot of work. Instead we integrate between the discontinuities.
		//   2) by sampling per-knot-interval, we ensure we always place a sample at each knot
		//      value. If we don't do this, we can "miss" the sharp corners at duplicate knots.
		//   3) within each interval, we compute arc length and # of steps, but then sample
		//      by subdividing the T-interval. This is not precise arc-length sampling but
		//      is closer than uniform-T along the curve. And it means we don't have to
		//      do an arc-length evaluation for each point, which is very expensive!!
		public static VectorArray2d SampleNURBSHybrid(NURBSCurve2 curve, double fSpacing)
		{
			List<double> intervals = curve.GetParamIntervals();
			int N = intervals.Count-1;

			VectorArray2d[] spans = new VectorArray2d[N];
			int nTotal = 0;

			for ( int i = 0; i < N; ++i ) {
				double t0 = intervals[i];
				double t1 = intervals[i+1];
				double fLen = curve.GetLength(t0,t1);

				int nSteps = Math.Max( (int)(fLen / fSpacing)+1, 2 );
				double div = 1.0 / nSteps;
				if ( curve.IsClosed == false && i == N-1 ) {
					nSteps++;
					div = 1.0 / nSteps-1;
				} 

				VectorArray2d vec = new VectorArray2d(nSteps);
				for ( int j = 0; j < nSteps; ++j ) {
					double a = (double)j * div;
					double t = (1-a)*t0 + (a)*t1;
					vec[j] = curve.SampleT(t);
				}
				spans[i] = vec;
				nTotal += nSteps;
			}

			VectorArray2d final = new VectorArray2d(nTotal);
			int iStart = 0;
			for ( int i = 0; i < N; ++i ) {
				final.Set(iStart, spans[i].Count, spans[i] );
				iStart += spans[i].Count;
			}

			return final;
		}



		public static VectorArray2d AutoSample(ParametricCurveSequence2 curves, double fSpacingLength, double fSpacingT)
		{
			int N = curves.Count;
			bool bClosed = curves.IsClosed;

			VectorArray2d[] vecs = new VectorArray2d[N];
			int i = 0;
			int nTotal = 0;
			foreach ( IParametricCurve2d c in curves.Curves ) {
				vecs[i] = AutoSample(c, fSpacingLength, fSpacingT);
				nTotal += vecs[i].Count;
				i++;
			}

			int nDuplicates = (bClosed) ? N : N-1;		// handle closed here...
			nTotal -= nDuplicates;

			VectorArray2d final = new VectorArray2d(nTotal);

			int k = 0;
			for ( int vi = 0; vi < N; ++vi ) {
				VectorArray2d vv = vecs[vi];
				// skip final vertex unless we are on last curve (because it is
				// the same as first vertex of next curve)
				int nStop = (bClosed || vi < N-1) ? vv.Count-1 : vv.Count;
				final.Set(k, nStop, vv);
				k += nStop;
			}

			return final;
		}
	}
}