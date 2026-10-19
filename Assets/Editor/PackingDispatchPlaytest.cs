using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ImmersiveTraining.TrainingInteractions;
using UnityEditor;
using UnityEngine;
using Valari.Data;
using Valari.Managers;
using Valari.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Headset-less Play-mode walkthrough of Module 2 (Packing and Dispatch), plus a smoke test of the M1_09 assessment.
/// It plays every M2 step by moving objects / calling the same entry points the hands use (no hand input), presses
/// Next, and checks each challenge completes in order - including the wrong-carton, product-before-fill,
/// unscanned-label, label-down, wrong-QC-answer and failed-quiz paths. Uses reflection on framework privates
/// (TutorialModalUI._container / _tutorialChallengeList, AssessmentManager._currentAssessmentDataList / _assessmentIndex).
///
/// M2: enter Play mode with TutorialManager auto-start = PackingAndDispatch, then Tools/Packing Dispatch/Test: M2
/// Walkthrough (Play mode). M1_09: run Test: M1_09 Smoke (Play mode) once the M2 walkthrough has finished (it starts
/// Order Picking itself and logs the trainer's walk state), or on a fresh Play session with auto-start = None.
/// Results go to the console and Temp/m2_playtest.txt.
/// </summary>
public static class PackingDispatchPlaytest
{
    private const string LogPath = "Temp/m2_playtest.txt";
    private static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
    private static double _resumeAt;
    private static StringBuilder _log;
    private static int _passes, _failures;

    [MenuItem("Tools/Packing Dispatch/Test: M2 Walkthrough (Play mode)")]
    public static void RunM2() => Begin(M2Flow());

    [MenuItem("Tools/Packing Dispatch/Test: M1_09 Smoke (Play mode)")]
    public static void RunM1() => Begin(M1Flow());

    private static void Begin(IEnumerator flow)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[M2Playtest] Enter Play mode first.");
            return;
        }

        Routines.Clear();
        Routines.Push(flow);
        _log = new StringBuilder();
        _passes = _failures = 0;
        _resumeAt = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Log("started");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish("left Play mode");
            return;
        }

        if (EditorApplication.timeSinceStartup < _resumeAt) return;
        try
        {
            if (Routines.Count == 0)
            {
                Finish("done");
                return;
            }

            IEnumerator top = Routines.Peek();
            if (!top.MoveNext())
            {
                Routines.Pop();
                return;
            }

            if (top.Current is IEnumerator nested) Routines.Push(nested);
            else if (top.Current is float seconds) _resumeAt = EditorApplication.timeSinceStartup + seconds;
        }
        catch (Exception e)
        {
            Fail("exception: " + e);
            Finish("aborted");
        }
    }

    private static void Finish(string why)
    {
        EditorApplication.update -= Tick;
        Routines.Clear();
        Log($"FINISHED ({why}): {_passes} passed, {_failures} failed");
        System.IO.File.WriteAllText(LogPath, _log.ToString());
        Debug.Log($"[M2Playtest] {why}: {_passes} passed, {_failures} failed - see {LogPath}");
    }

    private static void Log(string message)
    {
        _log?.AppendLine($"[{Time.time,7:0.0}] {message}");
        Debug.Log("[M2Playtest] " + message);
    }

    private static void Pass(string what)
    {
        _passes++;
        Log("PASS " + what);
    }

    private static void Fail(string what)
    {
        _failures++;
        Log("FAIL " + what);
    }

    private static void Check(bool ok, string what)
    {
        if (ok) Pass(what);
        else Fail(what);
    }

    private static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!Safe(condition))
        {
            if (EditorApplication.timeSinceStartup > end)
            {
                Fail(what + " (timed out)");
                yield break;
            }

            yield return 0.05f;
        }

        Pass(what);
    }

    private static bool Safe(Func<bool> condition)
    {
        try
        {
            return condition();
        }
        catch
        {
            return false;
        }
    }

    // ---------- framework access ----------

    private static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null ? (T)field.GetValue(target) : default;
    }

    private static TutorialModalUI Panel(string name) =>
        Resources.FindObjectsOfTypeAll<TutorialModalUI>().FirstOrDefault(p => p.gameObject.scene.IsValid() && p.name == name);

    private static ChallengeTutorialTrigger Trigger(string name) =>
        Resources.FindObjectsOfTypeAll<ChallengeTutorialTrigger>().FirstOrDefault(t => t.gameObject.scene.IsValid() && t.TriggerName == name);

    private static bool PanelShown(string name)
    {
        TutorialModalUI panel = Panel(name);
        var container = Field<GameObject>(panel, "_container");
        return panel != null && panel.gameObject.activeInHierarchy && container != null && container.activeInHierarchy;
    }

    private static bool ChallengeDone(string panel, string trigger)
    {
        var list = Field<List<TutorialChallengeData>>(Panel(panel), "_tutorialChallengeList");
        TutorialChallengeData challenge = list?.FirstOrDefault(c => c.ChallengeTriggerName == trigger);
        return challenge != null && challenge.IsComplete;
    }

    private static IEnumerator Next(string panel, string nextPanel)
    {
        Panel(panel).OnTriggerNextTutorial();
        if (nextPanel != null) yield return WaitFor(() => PanelShown(nextPanel), 15f, $"{nextPanel} shown");
        else yield return 1f;
    }

    private static IEnumerator Armed(string trigger) =>
        WaitFor(() => Trigger(trigger) != null && Trigger(trigger).IsArmed, 10f, $"{trigger} armed");

    private static IEnumerator Done(string panel, string trigger) =>
        WaitFor(() => ChallengeDone(panel, trigger), 5f, $"{trigger} complete");

    // ---------- scene helpers ----------

    private static void Place(Component component, Vector3 position, Quaternion rotation)
    {
        component.transform.SetPositionAndRotation(position, rotation);
        Rigidbody body = component.GetComponent<Rigidbody>();
        if (body == null) return;
        body.position = position;
        body.rotation = rotation;
        if (body.isKinematic) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private static IEnumerator DropPillows(VoidFillDispenser dispenser, PackingCarton carton, int count)
    {
        for (int i = 0; i < count; i++)
        {
            VoidFillPiece piece = null;
            double end = EditorApplication.timeSinceStartup + 5;
            while (piece == null && EditorApplication.timeSinceStartup < end)
            {
                for (int s = 0; s < dispenser.SlotCount && piece == null; s++) piece = dispenser.PieceInSlot(s);
                if (piece == null) yield return 0.1f;
            }

            if (piece == null)
            {
                Fail("no pillow available at the dispenser");
                yield break;
            }

            Place(piece, carton.DropPoint, Quaternion.identity);
            yield return 0.3f;
        }
    }

    private static IEnumerator Sweep(SeamTool tool, PackingCarton carton, int seam)
    {
        Rigidbody body = tool.GetComponent<Rigidbody>();
        bool wasKinematic = body != null && body.isKinematic;
        RigidbodyInterpolation wasInterpolation = body != null ? body.interpolation : RigidbodyInterpolation.None;
        if (body != null)
        {
            body.isKinematic = true;
            // Like a held tool (AutoHand turns interpolation off on grab): otherwise the old physics pose drags the
            // tool 2-3 cm back every frame and the sweep misses bins.
            body.interpolation = RigidbodyInterpolation.None;
        }

        tool.ForceActive = true;
        carton.SeamWorldEnds(seam, out Vector3 a, out Vector3 b);
        const int steps = 30;
        int startFrame = Time.frameCount;
        double startTime = EditorApplication.timeSinceStartup;
        float maxMiss = 0f, maxDrift = 0f;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 target = Vector3.Lerp(a, b, i / (float)steps) + Vector3.up * 0.01f;
            tool.transform.position += target - tool.HeadPosition;
            maxMiss = Mathf.Max(maxMiss, Vector3.Distance(tool.HeadPosition, target));

            // Hold each position for at least one rendered frame so the carton samples every step, however slow
            // Play mode runs (a slow frame rate once skipped most of a sweep).
            int frame = Time.frameCount;
            while (Time.frameCount <= frame) yield return 0.005f;
            maxDrift = Mathf.Max(maxDrift, Vector3.Distance(tool.HeadPosition, target)); // moved by something else?
        }

        Log($"  sweep {tool.Kind} seam {seam}: {Time.frameCount - startFrame} frames in " +
            $"{EditorApplication.timeSinceStartup - startTime:0.00} s, head miss {maxMiss * 100f:0.0} cm, " +
            $"drift after a frame {maxDrift * 100f:0.0} cm (interpolation={body?.interpolation}), taped={carton.IsTaped(seam)}");
        tool.ForceActive = false;
        if (body != null)
        {
            body.isKinematic = wasKinematic;
            body.interpolation = wasInterpolation;
        }
    }

    private static void AimScanner(ScannerTool scanner, Vector3 target)
    {
        Transform muzzle = Field<Transform>(scanner, "_muzzle");
        if (muzzle == null) muzzle = scanner.transform;
        Rigidbody body = scanner.GetComponent<Rigidbody>();
        if (body != null) body.isKinematic = true;
        Transform t = scanner.transform;
        t.rotation = Quaternion.FromToRotation(muzzle.forward, Vector3.down) * t.rotation;
        t.position += target + Vector3.up * 0.3f - muzzle.position;
    }

    private static bool QuizShowing()
    {
        AssessmentUI ui = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include);
        var questions = Field<GameObject>(ui, "_questionContainer");
        return questions != null && questions.activeInHierarchy;
    }

    private static IEnumerator AnswerQuiz(bool correct)
    {
        AssessmentManager manager = Object.FindFirstObjectByType<AssessmentManager>(FindObjectsInactive.Include);
        var questions = Field<List<AssessmentData>>(manager, "_currentAssessmentDataList");
        for (int i = 0; i < questions.Count; i++)
        {
            AssessmentData question = questions[Field<int>(manager, "_assessmentIndex")];
            string choice = correct
                ? question.Choices[question.AnswerIndex]
                : question.Choices[(question.AnswerIndex + 1) % question.Choices.Count];
            manager.SubmitAnswer(choice);
            yield return 0.05f;
        }

        yield return 0.3f;
        Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include).OnCloseButtonClicked();
        yield return 0.3f;
    }

    // ---------- M2 ----------

    private static IEnumerator M2Flow()
    {
        PackingStation station = Object.FindFirstObjectByType<PackingStation>();
        TutorialManager manager = Object.FindFirstObjectByType<TutorialManager>();
        Transform quizRoot = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include).transform;
        Vector3 quizHome = quizRoot.position;

        yield return WaitFor(() => station.OrderIndex == 0, 20f, "PackingAndDispatch started (auto-start)");
        if (station.OrderIndex != 0)
        {
            manager.StartTraining(TrainingID.PackingAndDispatch);
            yield return WaitFor(() => station.OrderIndex == 0, 3f, "PackingAndDispatch started manually");
        }

        PackingFeedback feedback = station.Feedback;
        VoidFillDispenser dispenser = Object.FindFirstObjectByType<VoidFillDispenser>();
        SeamTool tapeGun = SeamTool.All.First(t => t.Kind == SeamToolKind.Tape);
        SeamTool cutter = SeamTool.All.First(t => t.Kind == SeamToolKind.Cutter);

        // M2_01
        yield return WaitFor(() => PanelShown("M2_01_Welcome_Packing"), 15f, "M2_01 shown");
        yield return Next("M2_01_Welcome_Packing", "M2_02_Station_Layout");

        // M2_02 - gaze at the three signs
        yield return Armed("TRIGGER_M2_StationLayoutConfirmed");
        GazeDwellTarget[] signs = Object.FindObjectsByType<GazeDwellTarget>(FindObjectsSortMode.None);
        Check(signs.Length == 3, "three zone signs");
        var head = new GameObject("PlaytestHead").transform;
        foreach (GazeDwellTarget sign in signs) sign.HeadOverride = head;
        head.position = signs[0].transform.position + new Vector3(-2.5f, -0.3f, 0f);
        head.rotation = Quaternion.LookRotation(Vector3.left);
        yield return 1f;
        Check(!signs[0].IsFound, "looking away does not find a sign");
        foreach (GazeDwellTarget sign in signs)
        {
            head.position = sign.transform.position + new Vector3(-2.5f, -0.3f, 0f);
            head.LookAt(sign.transform.position);
            yield return WaitFor(() => sign.IsFound, 3f, $"{sign.name} found by gaze");
        }

        yield return Done("M2_02_Station_Layout", "TRIGGER_M2_StationLayoutConfirmed");
        yield return WaitFor(() => PanelShown("M2_03_Box_Selection"), 10f, "M2_02 auto-advanced to M2_03");
        foreach (GazeDwellTarget sign in signs) sign.HeadOverride = null;
        Object.Destroy(head.gameObject);

        // M2_03 - box selection
        yield return Armed("TRIGGER_M2_CorrectBoxSelected");
        PackingCarton small = station.SupplyCarton(CartonSize.Small);
        PackingCarton large = station.SupplyCarton(CartonSize.Large);
        PackingCarton medium = station.SupplyCarton(CartonSize.Medium);
        Vector3 onSpot = station.Spot.position + Vector3.up * 0.03f;
        Place(small, onSpot, station.Spot.rotation);
        yield return WaitFor(() => feedback.LastMessage.Contains("too small"), 3f, "Small carton refused as too small");
        yield return WaitFor(() => (small.transform.position - station.SupplySlot(CartonSize.Small).position).magnitude < 0.15f, 4f, "refused Small returned to the rack");
        Place(large, onSpot, station.Spot.rotation);
        yield return WaitFor(() => feedback.LastMessage.Contains("too big"), 3f, "Large carton refused as oversized");
        yield return 2f;
        Check(!ChallengeDone("M2_03_Box_Selection", "TRIGGER_M2_CorrectBoxSelected"), "wrong cartons do not complete M2_03");
        Place(medium, onSpot, station.Spot.rotation);
        yield return WaitFor(() => station.ActiveCarton == medium && medium.IsLocked, 3f, "Medium locked on the packing spot");
        yield return WaitFor(() => station.SupplyCarton(CartonSize.Medium) != null && station.SupplyCarton(CartonSize.Medium) != medium, 2f, "rack restocked with a new Medium");
        yield return Done("M2_03_Box_Selection", "TRIGGER_M2_CorrectBoxSelected");
        yield return Next("M2_03_Box_Selection", "M2_04_Void_Fill_Application");

        // M2_04 - void fill
        yield return Armed("TRIGGER_M2_VoidFillBase");
        PackingCarton box = medium;
        PackableItem speaker = station.LooseOrderItems().FirstOrDefault();
        Check(speaker != null, "order 1 product on the bench");
        Place(speaker, box.DropPoint, Quaternion.identity);
        yield return WaitFor(() => feedback.LastMessage.Contains("base of the box first"), 3f, "product refused before the base fill");
        Check(!speaker.IsPacked, "product not packed before the base fill");
        yield return DropPillows(dispenser, box, 2);
        yield return Done("M2_04_Void_Fill_Application", "TRIGGER_M2_VoidFillBase");
        yield return DropPillows(dispenser, box, 1);
        yield return WaitFor(() => feedback.LastMessage.Contains("Place the item"), 2f, "extra pillow refused until the product is in");
        Check(box.Fill.Fill == 2, "refused pillow not counted");
        Place(speaker, box.DropPoint, Quaternion.identity);
        yield return WaitFor(() => speaker.IsPacked && box.ItemCount == 1, 3f, "product packed on the base layer");
        Check(speaker.transform.IsChildOf(box.transform), "packed speaker is a child of the box");
        Vector3 speakerInBox = box.transform.InverseTransformPoint(speaker.transform.position);
        Func<bool> speakerInPlace = () =>
            Vector3.Distance(speaker.transform.position, box.transform.TransformPoint(speakerInBox)) < 0.02f;
        yield return DropPillows(dispenser, box, 3);
        yield return WaitFor(() => box.IsFull && box.IsClosed, 3f, "box full and flaps closed");
        yield return Done("M2_04_Void_Fill_Application", "TRIGGER_M2_VoidFillTop");
        yield return Next("M2_04_Void_Fill_Application", "M2_05_Sealing_the_Box");

        // M2_05 - H-tape
        yield return Armed("TRIGGER_M2_BoxSealed");
        Vector3 tapeHome = tapeGun.transform.position;
        Quaternion tapeHomeRotation = tapeGun.transform.rotation;
        yield return Sweep(tapeGun, box, PackingCarton.SeamCentre);
        yield return 0.3f;
        Check(box.IsTaped(PackingCarton.SeamCentre) && !box.IsSealed, "centre strip alone does not seal");
        Check(!ChallengeDone("M2_05_Sealing_the_Box", "TRIGGER_M2_BoxSealed"), "M2_05 not complete with one strip");
        yield return Sweep(tapeGun, box, PackingCarton.SeamEndA);
        yield return Sweep(tapeGun, box, PackingCarton.SeamEndB);
        yield return WaitFor(() => box.IsSealed, 2f, "H-tape seals the box");
        yield return Done("M2_05_Sealing_the_Box", "TRIGGER_M2_BoxSealed");
        Place(tapeGun, tapeHome, tapeHomeRotation);
        yield return Next("M2_05_Sealing_the_Box", "M2_06_Label_Verification");

        // M2_06 - label
        yield return Armed("TRIGGER_M2_LabelVerified");
        ShippingLabel label = Object.FindFirstObjectByType<ShippingLabel>();
        Place(label, box.TopPoint, Quaternion.identity);
        yield return WaitFor(() => feedback.LastMessage.Contains("Scan the label"), 2f, "unscanned label refused");
        Check(!box.IsLabelled, "label not applied before scanning");
        ScannerTool scanner = Object.FindObjectsByType<ScannerTool>(FindObjectsSortMode.None).First(s => s.ExpectedSku == "LBL-ORD-2001");
        yield return 0.3f;
        AimScanner(scanner, label.transform.position);
        scanner.Scan();
        yield return WaitFor(() => label.IsVerified, 2f, "scan verifies the label");
        yield return Done("M2_06_Label_Verification", "TRIGGER_M2_LabelVerified");
        Place(label, box.TopPoint, Quaternion.identity);
        yield return WaitFor(() => box.IsLabelled && label.IsApplied, 3f, "label applied to the top face");
        yield return Done("M2_06_Label_Verification", "TRIGGER_M2_LabelApplied");
        yield return WaitFor(() => !box.IsLocked, 2f, "labelled box released for carrying");
        Rigidbody scannerBody = scanner.GetComponent<Rigidbody>();
        if (scannerBody != null) scannerBody.isKinematic = false;
        yield return Next("M2_06_Label_Verification", "M2_07_Conveyor_Loading");

        // M2_07 - conveyor
        yield return Armed("TRIGGER_M2_BoxOnConveyor");
        ConveyorLoadZone zone = Object.FindFirstObjectByType<ConveyorLoadZone>();
        Place(box, zone.LoadPoint + Vector3.up * (box.InnerSize.y + 0.1f), Quaternion.Euler(180f, 0f, 0f));
        yield return WaitFor(() => feedback.LastMessage.Contains("must face UP"), 5f, "label-down box refused");
        Check(!box.IsOnConveyor, "label-down box not taken");
        Check(speakerInPlace(), "packed speaker moved with the box when it was carried and turned over");
        Place(box, zone.LoadPoint, Quaternion.identity);
        yield return WaitFor(() => box.IsOnConveyor, 5f, "label-up box taken by the conveyor");
        Vector3 boxAtLoad = box.transform.position;
        yield return 1.5f;
        Log($"  conveyor: box moved {Vector3.Distance(box.transform.position, boxAtLoad):0.00} m, speaker off its spot by " +
            $"{Vector3.Distance(speaker.transform.position, box.transform.TransformPoint(speakerInBox)) * 100f:0.0} cm, " +
            $"speaker parent={(speaker.transform.parent ? speaker.transform.parent.name : "none")}, " +
            $"interpolation={speaker.GetComponent<Rigidbody>().interpolation}");
        Check(speakerInPlace(), "packed speaker rides the conveyor inside the box");
        yield return Done("M2_07_Conveyor_Loading", "TRIGGER_M2_BoxOnConveyor");
        yield return WaitFor(() => station.OrderIndex == 1, 15f, "box dispatched and order 2 started");
        Check(station.LooseOrderItems().Count == 3, "order 2's three products on the bench");
        yield return Next("M2_07_Conveyor_Loading", "M2_08_Multi_Item_Consolidation");

        // M2_08 - multi-item order
        yield return Armed("TRIGGER_M2_MultiItemPacked");
        PackingCarton medium2 = station.SupplyCarton(CartonSize.Medium);
        PackingCarton large2 = station.SupplyCarton(CartonSize.Large);
        Place(medium2, onSpot, station.Spot.rotation);
        yield return WaitFor(() => feedback.LastMessage.Contains("too small"), 3f, "Medium refused for the three-item order");
        yield return 2f;
        Place(large2, onSpot, station.Spot.rotation);
        yield return WaitFor(() => station.ActiveCarton == large2 && large2.IsLocked, 3f, "Large locked for order 2");
        List<PackableItem> products = station.LooseOrderItems();
        Place(products[0], large2.DropPoint, Quaternion.identity);
        yield return WaitFor(() => feedback.LastMessage.Contains("base of the box first"), 3f, "order 2 product refused before base fill");
        yield return DropPillows(dispenser, large2, 2);
        foreach (PackableItem product in products)
        {
            Place(product, large2.DropPoint, Quaternion.identity);
            yield return WaitFor(() => product.IsPacked, 3f, $"{product.DisplayName} packed");
        }

        Check(!ChallengeDone("M2_08_Multi_Item_Consolidation", "TRIGGER_M2_MultiItemPacked"), "M2_08 waits for the top fill");
        yield return DropPillows(dispenser, large2, 3);
        yield return WaitFor(() => large2.IsFull, 3f, "Large carton full");
        yield return Done("M2_08_Multi_Item_Consolidation", "TRIGGER_M2_MultiItemPacked");
        yield return Next("M2_08_Multi_Item_Consolidation", "M2_09_Error_Identification");

        // M2_09 - QC
        yield return Armed("TRIGGER_M2_ErrorIdentified");
        QcReview qc = Object.FindFirstObjectByType<QcReview>();
        PackingCarton qcBox = qc.Carton;
        qc.ButtonFor(QcElement.Seal).onClick.Invoke();
        yield return WaitFor(() => qc.ResultText.Contains("is correct"), 1f, "wrong answer (seal) explained");
        Check(!qc.IsIdentified, "wrong answer does not identify the fault");
        qc.ButtonFor(QcElement.VoidFill).onClick.Invoke();
        yield return WaitFor(() => qc.ResultText.Contains("open the box"), 1f, "void-fill answer needs the box opened first");
        yield return Sweep(cutter, qcBox, PackingCarton.SeamCentre);
        yield return WaitFor(() => qcBox.HasBeenOpened && !qcBox.IsClosed, 2f, "box cutter opens the QC box");
        Func<string, bool> qcPartShown = path => qcBox.transform.Find(path).gameObject.activeSelf;
        Check(!qcPartShown("LabelVisual"), "QC label hidden while the box is open");
        Check(!qcPartShown("Tape/Tape_EndA") && !qcPartShown("Tape/Tape_EndB"), "QC end tape hidden while the box is open");
        qc.ButtonFor(QcElement.VoidFill).onClick.Invoke();
        yield return WaitFor(() => qc.IsIdentified, 1f, "void fill identified");
        yield return Done("M2_09_Error_Identification", "TRIGGER_M2_ErrorIdentified");
        yield return DropPillows(dispenser, qcBox, 4);
        yield return WaitFor(() => qcBox.IsFull && qcBox.IsClosed, 3f, "QC box filled to full and closed");
        Check(qcPartShown("LabelVisual"), "QC label shown again once the box is closed");
        Check(qcPartShown("Tape/Tape_EndA") && qcPartShown("Tape/Tape_EndB"), "QC end tape shown again once the box is closed");
        yield return Sweep(tapeGun, qcBox, PackingCarton.SeamCentre);
        yield return WaitFor(() => qcBox.IsSealed, 2f, "centre seam re-taped");
        yield return Done("M2_09_Error_Identification", "TRIGGER_M2_ErrorCorrected");
        yield return Next("M2_09_Error_Identification", "M2_10_Assessment");

        // M2_10 - assessment
        Transform anchor = GameObject.Find("M2_PackingStation/QuizAnchor_M2").transform;
        yield return WaitFor(() => (quizRoot.position - anchor.position).magnitude < 0.01f, 3f, "quiz moved to the packing station");
        yield return WaitFor(QuizShowing, 3f, "quiz launched at M2_10");
        yield return Armed("TRIGGER_M2_AssessmentComplete");
        yield return AnswerQuiz(false);
        AssessmentFailPanel failPanel = Object.FindFirstObjectByType<AssessmentFailPanel>(FindObjectsInactive.Include);
        yield return WaitFor(() => failPanel.gameObject.activeInHierarchy, 2f, "failing score shows the fail/retry screen");
        Check(!ChallengeDone("M2_10_Assessment", "TRIGGER_M2_AssessmentComplete"), "failed quiz does not complete M2_10");
        failPanel.OnRetry();
        yield return WaitFor(QuizShowing, 2f, "retake relaunches the quiz");
        yield return AnswerQuiz(true);
        yield return Done("M2_10_Assessment", "TRIGGER_M2_AssessmentComplete");
        yield return WaitFor(() => PanelShown("M2_10_Assessment"), 2f, "M2_10 modal shown again after the pass");
        yield return Next("M2_10_Assessment", "M2_11_Module_Complete");
        yield return WaitFor(() => (quizRoot.position - quizHome).magnitude < 0.01f, 2f, "quiz moved back to the spawn");

        // M2_11
        yield return Next("M2_11_Module_Complete", null);
        yield return 1f;
        Check(!PanelShown("M2_11_Module_Complete"), "module complete closed");
        Check(!QuizShowing(), "no quiz after the module-complete screen");
    }

    // ---------- M1_09 ----------

    private static IEnumerator M1Flow()
    {
        TutorialManager manager = Object.FindFirstObjectByType<TutorialManager>();
        Transform quizRoot = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include).transform;
        var trainer = Object.FindFirstObjectByType<Valari.FoundryTraining.Controller.TrainerController>();
        var agent = trainer.GetComponent<UnityEngine.AI.NavMeshAgent>();
        string TrainerState() =>
            $"trainer walking={trainer.IsWalking} pos={trainer.transform.position:F2} yaw={trainer.transform.eulerAngles.y:F0} " +
            $"remaining={agent.remainingDistance:F2} stop={agent.stoppingDistance:F2} cam={(Camera.main ? Camera.main.transform.position.ToString("F2") : "none")}";
        Log("before start: " + TrainerState());
        manager.StartTraining(TrainingID.OrderPicking);
        for (int i = 0; i < 6; i++)
        {
            yield return 0.5f;
            Log($"+{(i + 1) * 0.5f:0.0}s: " + TrainerState());
        }

        yield return WaitFor(() => PanelShown("M1_01_Welcome_Order_Picking"), 10f, "Order Picking started");
        for (int i = 0; i < 8; i++)
        {
            manager.OpenNextTutorialModal();
            yield return 0.3f;
        }

        yield return WaitFor(() => Panel("M1_09_Assessment") != null && Panel("M1_09_Assessment").gameObject.activeInHierarchy, 5f, "M1_09 is the 9th Order Picking step");
        Transform anchor = GameObject.Find("Canvas/OrderPicking/QuizAnchor_M1").transform;
        yield return WaitFor(() => (quizRoot.position - anchor.position).magnitude < 0.01f, 3f, "M1 quiz moved to the A aisle");
        yield return WaitFor(QuizShowing, 3f, "M1 quiz launched");
        yield return Armed("TRIGGER_M1_AssessmentComplete");
        yield return AnswerQuiz(true);
        yield return Done("M1_09_Assessment", "TRIGGER_M1_AssessmentComplete");
        yield return WaitFor(() => PanelShown("M1_09_Assessment"), 2f, "M1_09 modal shown again after the pass");
    }
}
