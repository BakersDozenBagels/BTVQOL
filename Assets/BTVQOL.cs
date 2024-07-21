using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System;
using System.Linq;

[RequireComponent(typeof(KMService))]
public class BTVQOL : MonoBehaviour
{
    private static BTVQOL Instance { get; set; }
    private static bool _harmedBTV, _harmedWAR;
    private static Type _sceneManager;
    private static Action _cameraZoomReset = () => { };
    private static Harmony _harm = new Harmony("BTVQOL.BakersDozenBagels.Ktane");

    private void Start()
    {
        if (Instance != null)
        {
            Debug.Log("[BadTV QOL] Error: duplicated service.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

#if UNITY_EDITOR
        if (Application.isEditor)
            return;
#endif

        StartCoroutine(FixBTV());
        StartCoroutine(FixWAR());
    }

    private IEnumerator FixWAR()
    {
        if (_harmedWAR)
            yield break;

        Type warType;
        do
        {
            warType = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(a => GetSafeTypes(a))
                .FirstOrDefault(t => t != null && t.Name.Equals("WAR"));
            yield return new WaitForSeconds(15f);
        }
        while (warType == null);

        Type displayClass = warType
            .GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t =>
                t != null &&
                t.Name.Contains("DETONATE")
                && typeof(IEnumerator).IsAssignableFrom(t));

        if (displayClass == null)
            throw new Exception("No relevant display class found in WAR. Please contact Bagels so he can write a better mod to handle this.");

        MethodInfo method = displayClass.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public);
        _harm.Patch(method, transpiler: new HarmonyMethod(typeof(BTVQOL).GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static)));
        Debug.Log("[BadTV QOL] Successfully modified WAR.");
        _harmedWAR = true;
    }

    private IEnumerator FixBTV()
    {
        if (_harmedBTV)
            yield break;

        StartCoroutine(FindCameraZoom());

        do
        {
            _sceneManager = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(a => GetSafeTypes(a))
                .FirstOrDefault(t => t != null && t.Name.Equals("SceneManager"));
            yield return new WaitForSeconds(1f);
        }
        while (_sceneManager == null);

        Type btvType;
        do
        {
            btvType = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(a => GetSafeTypes(a))
                .FirstOrDefault(t => t != null && t.Name.Equals("BTVScript"));
            yield return new WaitForSeconds(15f);
        }
        while (btvType == null);

        Type displayClass = btvType
            .GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t =>
                t != null &&
                t.Name.Contains("Death")
                && typeof(IEnumerator).IsAssignableFrom(t));

        if (displayClass == null)
            throw new Exception("No relevant display class found in BTVScript. Please contact Bagels so he can write a better mod to handle this.");

        MethodInfo method = displayClass.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public);
        _harm.Patch(method, transpiler: new HarmonyMethod(typeof(BTVQOL).GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static)));
        Debug.Log("[BadTV QOL] Successfully modified BadTV.");
        _harmedBTV = true;
    }

    private IEnumerator FindCameraZoom()
    {
        Type zoom;
        do
        {
            zoom = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(a => GetSafeTypes(a))
                .FirstOrDefault(t => t != null && t.Name.Equals("CamZoom"));
            yield return new WaitForSeconds(1f);
        }
        while (zoom == null);

        MethodInfo czMethod = zoom
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(mi => mi.GetParameters().Length == 1 && mi.GetParameters()[0].ParameterType == typeof(KMGameInfo.State));
        if (czMethod == null)
            throw new Exception("No relevant method found in CamZoom. Please contact Bagels so he can write a better mod to handle this.");

        _cameraZoomReset = () =>
        {
            UnityEngine.Object cz = FindObjectOfType(zoom);
            if (cz == null)
                return;
            czMethod.Invoke(cz, new object[] { KMGameInfo.State.Transitioning });
        };
        Debug.Log("[BadTV QOL] Found Camera Zoom, applying fix.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> orig)
    {
        MethodInfo a = typeof(Application).GetMethod("Quit", BindingFlags.Public | BindingFlags.Static);
        MethodInfo b = typeof(BTVQOL).GetMethod("Quit", BindingFlags.NonPublic | BindingFlags.Static);
        return orig.MethodReplacer(a, b);
    }

    private static void Quit()
    {
        if (Instance == null)
        {
            Debug.Log("[BadTV QOL] Error: no service found. Using original behavior.");
            Application.Quit();
            return;
        }

        Debug.Log("[BadTV QOL] Returning to setup room.");
        Camera.main.targetTexture = null;
        Camera.main.ResetProjectionMatrix();
        object sm = _sceneManager
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null, new object[0]);
        object gs = _sceneManager
            .GetProperty("GameplayState", BindingFlags.Public | BindingFlags.Instance)
            .GetValue(sm, new object[0]);
        if (gs != null)
            gs.GetType()
                .GetMethod("ExitState", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(gs, new object[0]);
        _sceneManager
            .GetMethod("ReturnToSetupState", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(sm, new object[0]);

        _cameraZoomReset();
    }

    private Type[] GetSafeTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
        catch (Exception)
        {
            return new Type[0];
        }
    }
}
