using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class CutoutObjectTests
{
    private readonly List<Object> cleanup = new List<Object>();
    private CutoutObject cutout;

    [SetUp]
    public void SetUp()
    {
        var camera = Track(new GameObject("Test Camera", typeof(Camera)));
        var target = Track(new GameObject("Test Target"));
        target.transform.position = new Vector3(0, 0, 10);
        cutout = camera.AddComponent<CutoutObject>();
        cutout.enabled = false;
        typeof(CutoutObject).GetField("targetObject", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(cutout, target.transform);
        typeof(CutoutObject).GetField("wallMask", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(cutout, (LayerMask)(1 << 6));
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; --i)
            if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    [Test]
    public void Update_SkipsColliderWithoutRenderer()
    {
        CreateCollider();
        Assert.DoesNotThrow(UpdateCutout);
    }

    [Test]
    public void Update_WritesCutoutParametersToEveryWallMaterial()
    {
        var wall = CreateWall();
        UpdateCutout();
        AssertCutout(wall);
    }

    [Test]
    public void Update_StillUpdatesWallWhenColliderOnlyObjectIsAlsoHit()
    {
        CreateCollider();
        var wall = CreateWall();
        Assert.DoesNotThrow(UpdateCutout);
        AssertCutout(wall);
    }

    [Test]
    public void Update_UsesHitColliderRatherThanRigidbodyTransform()
    {
        var parent = Track(new GameObject("Physics Parent", typeof(Rigidbody)));
        parent.GetComponent<Rigidbody>().isKinematic = true;
        var wall = CreateWall();
        wall.transform.SetParent(parent.transform, true);
        Assert.DoesNotThrow(UpdateCutout);
        AssertCutout(wall);
    }

    private void CreateCollider()
    {
        var collider = Track(new GameObject("Collider_05", typeof(BoxCollider)));
        collider.layer = 6;
        collider.transform.position = new Vector3(0, 0, 3);
    }

    [Test]
    public void Update_FindsVisibleSiblingOfEmptyColliderRenderer()
    {
        var root = Track(new GameObject("Wall Root"));
        root.layer = 6;
        var collider = Track(new GameObject("Collider_05", typeof(BoxCollider), typeof(MeshRenderer)));
        collider.layer = 6;
        collider.transform.SetParent(root.transform);
        collider.transform.position = new Vector3(0, 0, 4);
        var wall = CreateWall();
        Object.DestroyImmediate(wall.GetComponent<Collider>());
        wall.transform.SetParent(root.transform);
        var unrelatedWall = CreateWall();
        unrelatedWall.transform.position = new Vector3(20, 0, 4);
        UpdateCutout();
        AssertCutout(wall);
        Assert.That(unrelatedWall.sharedMaterial.GetFloat("_CutoutSize"), Is.Zero);
    }

    [Test]
    public void Update_ClosesHoleWhenWallStopsOccluding()
    {
        var wall = CreateWall();
        UpdateCutout();
        AssertCutout(wall);
        wall.transform.position = new Vector3(20, 0, 6);
        UpdateCutout();
        Assert.That(wall.sharedMaterial.GetFloat("_CutoutSize"), Is.Zero);
    }

    [Test]
    public void OnDisable_ClosesExistingHole()
    {
        var wall = CreateWall();
        UpdateCutout();
        cutout.SendMessage("OnDisable", SendMessageOptions.DontRequireReceiver);
        Assert.That(wall.sharedMaterial.GetFloat("_CutoutSize"), Is.Zero);
    }

    [Test]
    public void OnDisable_RestoresOriginalMaterial()
    {
        var wall = CreateWall();
        var original = wall.sharedMaterial;
        UpdateCutout();
        cutout.SendMessage("OnDisable", SendMessageOptions.DontRequireReceiver);
        Assert.That(wall.sharedMaterial, Is.SameAs(original));
    }

    [Test]
    public void Update_ReusesHoleAfterWallStopsOccludingAndReturns()
    {
        var wall = CreateWall();
        var original = wall.sharedMaterial;
        UpdateCutout();
        var instance = wall.sharedMaterial;
        wall.transform.position = new Vector3(20, 0, 6);
        UpdateCutout();
        Assert.That(wall.sharedMaterial, Is.SameAs(original));
        wall.transform.position = new Vector3(0, 0, 6);
        UpdateCutout();
        Assert.That(wall.sharedMaterial, Is.SameAs(instance));
        AssertCutout(wall);
    }

    [Test]
    public void Update_ConvertsOrdinaryMaterialAndPreservesTextureUV()
    {
        var wall = CreateWall();
        var source = Track(new Material(Shader.Find("Hidden/CutoutSource")));
        var texture = Track(new Texture2D(2, 2));
        source.SetTexture("_BaseMap", texture);
        source.SetTextureScale("_BaseMap", new Vector2(2, 3));
        source.SetTextureOffset("_BaseMap", new Vector2(0.2f, 0.3f));
        wall.sharedMaterials = new[] { source };
        var shaderField = typeof(CutoutObject).GetField("cutoutShader", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(shaderField, Is.Not.Null, "The camera needs a serialized reference to the cutout shader.");
        shaderField.SetValue(cutout, Shader.Find("Hidden/CutoutRegression"));
        UpdateCutout();
        Assert.That(wall.sharedMaterial.shader.name, Is.EqualTo("Hidden/CutoutRegression"));
        Assert.That(wall.sharedMaterial.GetTexture("_MainTexture"), Is.SameAs(texture));
        Assert.That((Vector2)wall.sharedMaterial.GetVector("_Tiling"), Is.EqualTo(new Vector2(2, 3)));
        Assert.That((Vector2)wall.sharedMaterial.GetVector("_Offset"), Is.EqualTo(new Vector2(0.2f, 0.3f)));
        Assert.That(source.shader.name, Is.EqualTo("Hidden/CutoutSource"));
        Assert.That(wall.sharedMaterial.GetFloat("_CutoutSize"), Is.EqualTo(0.1f).Within(0.0001f));
    }

    private Renderer CreateWall()
    {
        var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        wall.layer = 6;
        wall.transform.position = new Vector3(0, 0, 6);
        var renderer = wall.GetComponent<Renderer>();
        var shader = Shader.Find("Hidden/CutoutRegression");
        Assert.That(shader, Is.Not.Null);
        renderer.sharedMaterials = new[] { Track(new Material(shader)), Track(new Material(shader)) };
        return renderer;
    }

    private void UpdateCutout()
    {
        Physics.SyncTransforms();
        try
        {
            typeof(CutoutObject).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cutout, null);
        }
        finally
        {
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && !cleanup.Contains(material)) cleanup.Add(material);
        }
    }

    private static void AssertCutout(Renderer wall)
    {
        Assert.That(wall.sharedMaterials.Length, Is.EqualTo(2));
        foreach (var material in wall.sharedMaterials)
        {
            Assert.That(material.GetFloat("_CutoutSize"), Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(material.GetFloat("_FalloffSize"), Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(material.GetVector("_CutoutPos").x, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }

    private T Track<T>(T value) where T : Object
    {
        cleanup.Add(value);
        return value;
    }
}
