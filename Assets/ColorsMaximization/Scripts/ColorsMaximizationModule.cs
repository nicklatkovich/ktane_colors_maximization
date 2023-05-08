using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class ColorsMaximizationModule : MonoBehaviour {
	public static readonly Dictionary<Color, string> colorNames = new Dictionary<Color, string> {
		{ Color.red, "Red" },
		{ Color.green, "Green" },
		{ Color.blue, "Blue" },
		{ Color.magenta, "Magenta" },
		{ Color.yellow, "Yellow" },
		{ Color.white, "White" },
	};

	private static readonly Color[] allColors = { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.white };
	private static readonly Color[][] rules = {
		new Color[] { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.white },
		new Color[] { Color.green, Color.red, Color.yellow, Color.magenta, Color.blue, Color.white },
		new Color[] { Color.white, Color.blue, Color.red, Color.green, Color.magenta, Color.yellow },
		new Color[] { Color.magenta, Color.green, Color.blue, Color.red, Color.white, Color.yellow },
		new Color[] { Color.magenta, Color.yellow, Color.white, Color.green, Color.red, Color.blue },
		new Color[] { Color.white, Color.yellow, Color.magenta, Color.blue, Color.green, Color.red },
	};

	private static int moduleIdCounter = 1;

	private static int[] GenerateSouvenirQuestion(int correctAnswer, int min, int max, int maxStep) {
		int minResult = correctAnswer;
		int maxResult = correctAnswer;
		int[] result = new int[5];
		int discentsCount = Random.Range(0, 5);
		for (int i = 0; i < 5; i++) {
			int diff = Random.Range(1, maxStep + 1);
			if (discentsCount > i && minResult - diff >= min) {
				minResult -= diff;
				result[i] = minResult;
			} else if (maxResult + diff <= max) {
				maxResult += diff;
				result[i] = maxResult;
			} else return result;
		}
		return result;
	}

	private static HashSet<Color> GetHalfRandomColors() {
		HashSet<Color> result = new HashSet<Color>();
		Color[] colors = allColors.Clone() as Color[];
		for (int i = 0; i < colors.Length; i++) {
			int pos = Random.Range(0, colors.Length - 1);
			Color tmp = colors[i];
			colors[i] = colors[pos];
			colors[pos] = tmp;
		}
		int size = Random.Range(colors.Length / 2, colors.Length / 2 + 1 + (colors.Length % 2 == 0 ? 0 : 1));
		for (int i = 0; i < size; i++) result.Add(colors[i]);
		return result;
	}

	private const int WIDTH = 5;
	private const int HEIGHT = 4;

	public readonly string TwitchHelpMessage = string.Join(" | ", new string[] {
		"\"!{0} a1 2;2 15 submit\" to press buttons on coordinates, by number in reading order or with label SUBMIT",
		"\"!{0} activate\" to activate all inactive keys",
		"\"!{0} colorblind\" to enable/disable colorblind mode",
	});

	public GameObject ButtonsCollection;
	public TextMesh SubmitText;
	public KMAudio Audio;
	public KMBombInfo BombInfo;
	public KMBombModule BombModule;
	public KMColorblindMode ColorblindMode;
	public KMSelectable SelfSelectable;
	public KMSelectable SubmitButton;
	public ButtonComponent ButtonPrefab;

	private bool _colorblindModeEnabled = false;
	public bool colorblindModeEnabled {
		get { return _colorblindModeEnabled; }
		private set {
			if (value == _colorblindModeEnabled) return;
			_colorblindModeEnabled = value;
			buttons.ForEach(b => b.colorblindMode = value);
			SubmitText.color = value ? Color.white : Color.red;
		}
	}

	private bool _forceSolved = true;
	public bool forceSolved { get { return _forceSolved; } }

	private int _submittedScore = -1;
	public int submittedScore { get { return _submittedScore; } }

	public int[] souvenirSubmittedScoreWrongAnswers { get { return GenerateSouvenirQuestion(_submittedScore, 20, 120, 5); } }

	private HashSet<Color> _submittedColors;
	public HashSet<Color> submittedColors { get { return new HashSet<Color>(_submittedColors); } }

	private bool activated = false;
	private bool answerIsDynamic = false;
	private bool solved = false;
	private int moduleId;
	private List<ButtonComponent> buttons = new List<ButtonComponent>();
	private ButtonComponent[][] buttonsGrid = new ButtonComponent[0][] { };
	private Dictionary<Color, int> countOfColor = new Dictionary<Color, int>();

	public int[] GenerateSouvenirColorCountWrongAnswers(Color color) {
		return GenerateSouvenirQuestion(countOfColor[color], 0, 20, 1);
	}

	private void Start() {
		colorblindModeEnabled = ColorblindMode.ColorblindModeActive;
		moduleId = moduleIdCounter++;
		foreach (Color color in allColors) countOfColor[color] = 0;
		List<KMSelectable> children = new List<KMSelectable>();
		HashSet<Color> colorsActiveAtStart = GetHalfRandomColors();
		buttonsGrid = new ButtonComponent[WIDTH][];
		for (int x = 0; x < WIDTH; x++) {
			buttonsGrid[x] = new ButtonComponent[HEIGHT];
			for (int z = 0; z < HEIGHT; z++) {
				ButtonComponent button = Instantiate(ButtonPrefab);
				button.transform.parent = ButtonsCollection.transform;
				button.transform.localPosition = new Vector3(x * 0.02f, 0.015f, (HEIGHT - z - 1) * 0.02f);
				button.transform.localScale = new Vector3(0.0101f, 0.01f, 0.0101f);
				button.transform.localEulerAngles = Vector3.zero;
				Color color = allColors[Random.Range(0, allColors.Length)];
				button.primaryColor = color;
				countOfColor[color] += 1;
				button.active = colorsActiveAtStart.Contains(color);
				button.colorblindMode = ColorblindMode.ColorblindModeActive;
				buttons.Add(button);
				button.Selectable.Parent = SelfSelectable;
				children.Add(button.Selectable);
				buttonsGrid[x][z] = button;
			}
		}
		foreach (Color color in allColors) {
			string prettier = colorNames[color];
			int count = countOfColor[color];
			Debug.LogFormat("[Colors Maximization #{0}] {1} color count = {2}", moduleId, prettier, count);
		}
		children.Add(SubmitButton);
		SelfSelectable.Children = children.ToArray();
		SelfSelectable.UpdateChildren();
		SubmitButton.OnInteract += OnSubmitPressed;
		BombModule.OnActivate += OnActivate;
	}

	private void OnActivate() {
		int ruleNumber = GetRuleNumber();
		if (ruleNumber < 5 || !BombInfo.IsTwoFactorPresent()) {
			Debug.LogFormat("[Colors Maximization #{0}] Using rule #{1}", moduleId, ruleNumber);
			Debug.LogFormat("[Colors Maximization #{0}] \tColors sorted by score: {1}", moduleId, rules[ruleNumber - 1].Select(c => colorNames[c]).Join(","));
			GetExpectedScore(ruleNumber, true);
		} else {
			answerIsDynamic = true;
			Debug.LogFormat("[Colors Maximization #{0}] Rule is dynamic", moduleId);
			Debug.LogFormat("[Colors Maximization #{0}] When rule #5 is used:", moduleId);
			Debug.LogFormat("[Colors Maximization #{0}] \tColors sorted by score: {1}", moduleId, rules[4].Select(c => colorNames[c]).Join(","));
			GetExpectedScore(5, true);
			Debug.LogFormat("[Colors Maximization #{0}] When rule #6 is used:", moduleId);
			Debug.LogFormat("[Colors Maximization #{0}] \tColors sorted by score: {1}", moduleId, rules[5].Select(c => colorNames[c]).Join(","));
			GetExpectedScore(6, true);
		}
		foreach (ButtonComponent button in buttons) {
			ButtonComponent closure = button;
			button.Selectable.OnInteract += () => OnButtonPressed(closure);
		}
		activated = true;
	}

	private bool OnButtonPressed(ButtonComponent button) {
		if (solved) return false;
		Color color = button.primaryColor;
		bool activate = !button.active;
		foreach (ButtonComponent otherButton in buttons) if (otherButton.primaryColor == color) otherButton.active = activate;
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
		return false;
	}

	private bool OnSubmitPressed() {
		if (solved) return false;
		int ruleNumber = GetRuleNumber();
		if (answerIsDynamic) Debug.LogFormat("[Colors Maximization #{0}] Submitted on rule #{1}", moduleId, ruleNumber);
		Color[] colors = rules[ruleNumber - 1];
		Dictionary<Color, int> scoreOfColor = new Dictionary<Color, int>();
		for (int i = 0; i < colors.Length; i++) scoreOfColor[colors[i]] = i + 1;
		int expectedScore = GetExpectedScore(ruleNumber);
		int submitedScore = 0;
		HashSet<Color> submittedColors = new HashSet<Color>();
		foreach (ButtonComponent button in buttons) {
			if (!button.active) continue;
			submitedScore += scoreOfColor[button.primaryColor];
			submittedColors.Add(button.primaryColor);
		}
		Debug.LogFormat("[Colors Maximization #{0}] Submited colors: {1}", moduleId, submittedColors.Select(c => colorNames[c]).Join(","));
		LogColorsDifferenceCollision(scoreOfColor, submittedColors);
		Debug.LogFormat("[Colors Maximization #{0}] Submited score: {1}", moduleId, submitedScore);
		if (submitedScore == expectedScore) {
			Debug.LogFormat("[Colors Maximization #{0}] Submited score equals expected score. Module solved", moduleId);
			solved = true;
			_submittedScore = submitedScore;
			this._submittedColors = submittedColors;
			_forceSolved = false;
			BombModule.HandlePass();
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, this.transform);
			StartCoroutine(ShuffleKeys());
		} else {
			Debug.LogFormat("[Colors Maximization #{0}] Submited score not equals expected score. Strike", moduleId);
			BombModule.HandleStrike();
		}
		return false;
	}

	public IEnumerator ProcessTwitchCommand(string command) {
		if (!activated) {
			yield return "sendtochat {0}, !{1} not activated";
			yield break;
		}
		command = command.Trim().ToLower();
		if (command == "colorblind") {
			yield return null;
			colorblindModeEnabled = !colorblindModeEnabled;
			yield break;
		}
		if (Regex.IsMatch(command, @"^activate( *all)?$")) {
			yield return null;
			Dictionary<Color, KMSelectable> keys = new Dictionary<Color, KMSelectable>();
			for (int y = 0; y < HEIGHT; y++) {
				for (int x = 0; x < WIDTH; x++) {
					ButtonComponent button = buttonsGrid[x][y];
					if (!button.active && !keys.ContainsKey(button.primaryColor)) keys[button.primaryColor] = button.GetComponent<KMSelectable>();
				}
			}
			yield return keys.Values.ToArray();
			yield break;
		}
		KMSelectable[] parsedCoords = ParseButtonsSet(command);
		if (parsedCoords != null) {
			yield return null;
			yield return parsedCoords;
			yield break;
		}
	}

	private void TwitchHandleForcedSolve() {
		if (solved) return;
		Debug.LogFormat("[Colors Maximization #{0}] Module force-solved", moduleId);
		solved = true;
		BombModule.HandlePass();
		StartCoroutine(ShuffleKeys());
	}

	private IEnumerator<object> ShuffleKeys() {
		HashSet<Color> fakeSubmittedColors = GetHalfRandomColors();
		int[] keysNumbers = new int[WIDTH * HEIGHT].Select((_, i) => i).ToArray();
		for (int i = 0; i < WIDTH * HEIGHT; i++) {
			int temp = keysNumbers[i];
			int rndIndex = Random.Range(0, WIDTH * HEIGHT);
			keysNumbers[i] = keysNumbers[rndIndex];
			keysNumbers[rndIndex] = temp;
		}
		foreach (int keyNumber in keysNumbers) {
			Color color = allColors[Random.Range(0, allColors.Length)];
			ButtonComponent button = buttonsGrid[keyNumber % WIDTH][keyNumber / WIDTH];
			button.primaryColor = color;
			button.active = fakeSubmittedColors.Contains(color);
			yield return new WaitForSeconds(.1f);
		}
	}

	private int GetRuleNumber() {
		if (BombInfo.GetBatteryCount() == 4) return 1;
		else if (BombInfo.GetOffIndicators().Count() == 2) return 2;
		else if (BombInfo.GetSerialNumberNumbers().Where(e => e % 2 == 0).Count() == 2) return 3;
		else if (BombInfo.IsPortPresent(Port.Serial)) return 4;
		else if (BombInfo.GetTwoFactorCodes().Any(e => e % 3 == 0 || e % 5 == 0)) return 5;
		return 6;
	}

	private KMSelectable ParseButton(string str) {
		if (str == "submit") return SubmitButton;
		Match match = Regex.Match(str, @"^([a-e])([1-4])$");
		if (match.Success) return buttonsGrid[match.Groups[1].Value[0] - 'a'][int.Parse(match.Groups[2].Value) - 1].Selectable;
		match = Regex.Match(str, @"^([1-5])[;,]([1-4])$");
		if (match.Success) return buttonsGrid[int.Parse(match.Groups[1].Value) - 1][int.Parse(match.Groups[2].Value) - 1].Selectable;
		if (Regex.IsMatch(str, @"^0|[1-9]\d*$")) {
			int index = int.Parse(str) - 1;
			int row = index / WIDTH;
			if (row >= HEIGHT) return null;
			return buttonsGrid[index % WIDTH][row].Selectable;
		}
		return null;
	}

	private KMSelectable[] ParseButtonsSet(string set) {
		if (set.StartsWith("press ")) set = set.Skip(6).Join("");
		string[] strings = set.Split(' ').Where(r => r.Length > 0).ToArray();
		KMSelectable[] result = new KMSelectable[strings.Count()];
		for (int i = 0; i < strings.Count(); i++) {
			KMSelectable selectable = ParseButton(strings[i]);
			if (selectable == null) return null;
			result[i] = selectable;
		}
		return result;
	}

	private int GetExpectedScore(int ruleNumber, bool debug = false) {
		Color[] colors = rules[ruleNumber - 1];
		int[] scores = new int[6];
		List<Color>[] answerExamples = new List<Color>[6];
		for (int i = 0; i < colors.Length; i++) {
			int score = i + 1;
			Color color = colors[i];
			int totalColorScore = score * countOfColor[color];
			scores[i] = totalColorScore;
			answerExamples[i] = new List<Color> { color };
			if (i > 1) {
				scores[i] += scores[i - 2];
				answerExamples[i].AddRange(answerExamples[i - 2]);
			}
			if (i > 0 && scores[i - 1] > scores[i]) {
				scores[i] = scores[i - 1];
				answerExamples[i] = new List<Color>(answerExamples[i - 1]);
			}
		}
		int expectedScore = scores[colors.Length - 1];
		List<Color> answerExample = answerExamples[answerExamples.Length - 1];
		if (scores[colors.Length - 2] > expectedScore) {
			expectedScore = scores[colors.Length - 2];
			answerExample = answerExamples[answerExamples.Length - 2];
		}
		if (debug) {
			Debug.LogFormat("[Colors Maximization #{0}] \tExpected score: {1}", moduleId, expectedScore);
			Debug.LogFormat("[Colors Maximization #{0}] \tAnswer example: {1}", moduleId, answerExample.Select(c => colorNames[c]).Join(","));
		}
		return expectedScore;
	}

	private void LogColorsDifferenceCollision(Dictionary<Color, int> scoreOfColor, HashSet<Color> submittedColors) {
		Color[] colors = submittedColors.ToArray();
		for (int i = 0; i < colors.Length; i++) {
			Color c1 = colors[i];
			for (int j = i + 1; j < colors.Length; j++) {
				Color c2 = colors[j];
				if (Mathf.Abs(scoreOfColor[c1] - scoreOfColor[c2]) != 1) continue;
				Debug.LogFormat("[Colors Maximization #{0}] Submited two colors with score difference of 1: {1} and {2}", moduleId, colorNames[c1], colorNames[c2]);
			}
		}
	}

	private void LogRule(int ruleNumber) {
		Debug.LogFormat("[Colors Maximization #{0}] Using rule #{1}", moduleId, ruleNumber);
	}
}
