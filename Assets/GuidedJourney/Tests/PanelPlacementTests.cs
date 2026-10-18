using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Group10.GuidedJourney.Tests
{
    public sealed class PanelPlacementTests
    {
        Type type;object placement;GameObject headGO,panelGO,parent;
        bool Place(float time,bool force=false)=>(bool)type.GetMethod("Place").Invoke(placement,new object[]{headGO.transform,panelGO.GetComponent<RectTransform>(),Vector3.forward,time,force});
        [SetUp] public void Setup(){type=Type.GetType("GuidancePanelPlacement, Assembly-CSharp",true);placement=Activator.CreateInstance(type);headGO=new GameObject("Math head");headGO.transform.position=new Vector3(0,1.6f,0);panelGO=new GameObject("Canvas math",typeof(RectTransform),typeof(Canvas));panelGO.GetComponent<RectTransform>().sizeDelta=new Vector2(870,270);}
        [TearDown]public void End(){Object.DestroyImmediate(headGO);Object.DestroyImmediate(panelGO);if(parent!=null)Object.DestroyImmediate(parent);}
        [Test]public void InitialPanelIsCenteredAndFacesViewer(){Assert.IsTrue(Place(0));Assert.AreEqual(0,panelGO.transform.position.x,.0001);Assert.That(panelGO.transform.position.y,Is.EqualTo(1.5).Within(.001));Assert.That(Vector3.Angle(panelGO.transform.forward,panelGO.transform.position-headGO.transform.position),Is.LessThan(.01));}
        [Test]public void LeftAndRightEdgesHaveEqualDistance(){Place(0);Vector3 a=panelGO.transform.position+panelGO.transform.right*.435f,b=panelGO.transform.position-panelGO.transform.right*.435f;Assert.That(Vector3.Distance(headGO.transform.position,a),Is.EqualTo(Vector3.Distance(headGO.transform.position,b)).Within(.0001));}
        [Test]public void SmallHeadMotionDoesNotMovePanelEveryFrame(){Place(0);Vector3 before=panelGO.transform.position;headGO.transform.rotation=Quaternion.Euler(0,5,0);Assert.IsFalse(Place(1));Assert.AreEqual(before,panelGO.transform.position);}
        [Test]public void LargeTurnAndWalkingRecenter(){Place(0);headGO.transform.rotation=Quaternion.Euler(0,70,0);Assert.IsTrue(Place(1));headGO.transform.position+=Vector3.right;Assert.IsTrue(Place(2));}
        [Test]public void InheritedNonuniformScaleCannotTiltOrStretchCanvas(){parent=new GameObject("Scaled owner");parent.transform.localScale=new Vector3(2,3,4);parent.transform.rotation=Quaternion.Euler(10,20,30);panelGO.transform.SetParent(parent.transform);Place(0);Assert.IsNull(panelGO.transform.parent);Assert.That(Vector3.Distance(panelGO.transform.lossyScale,Vector3.one*.001f),Is.LessThan(.00001));Assert.AreEqual(new Vector3(2,3,4),parent.transform.localScale);}
        [Test]public void VerticalViewProducesFinitePose(){headGO.transform.rotation=Quaternion.Euler(-90,0,0);Assert.IsTrue(Place(0));Assert.IsFalse(float.IsNaN(panelGO.transform.position.x));Assert.IsFalse(float.IsNaN(panelGO.transform.rotation.w));}
    }
}
