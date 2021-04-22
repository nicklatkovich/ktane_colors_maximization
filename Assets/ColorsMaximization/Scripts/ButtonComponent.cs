using UnityEngine;

public class ButtonComponent : MonoBehaviour {
	private static readonly Color DEFAULT_BUTTON_COLOR = new Color(.33f, .33f, .33f);

	public TextMesh Text;
	public Renderer Glass;
	public Renderer Button;
	public Shader SelectedShader;
	public Shader UnselectedShader;
	public KMSelectable Selectable;

	private bool _colorblindMode = false;
	public bool colorblindMode {
		get { return _colorblindMode; }
		set {
			if (_colorblindMode == value) return;
			_colorblindMode = value;
			OnChanged();
		}
	}

	private Color _primaryColor;
	public Color primaryColor {
		get { return _primaryColor; }
		set {
			if (_primaryColor == value) return;
			_primaryColor = value;
			OnChanged();
		}
	}

	private bool _active = true;
	public bool active {
		get { return _active; }
		set {
			if (_active == value) return;
			_active = value;
			OnChanged();
		}
	}

	private bool selected = false;

	private void Start() {
		Selectable.OnHighlight += () => {
			selected = true;
			Button.material.shader = SelectedShader;
			highlight();
		};
		Selectable.OnHighlightEnded += () => {
			selected = false;
			Button.material.shader = UnselectedShader;
			Button.material.SetColor("_Color", DEFAULT_BUTTON_COLOR);
		};
	}

	private void OnChanged() {
		if (!_colorblindMode || !_active) Text.text = "";
		else {
			Text.text = ColorsMaximizationModule.colorNames[_primaryColor].Substring(0, 1);
			Text.color = negative(_primaryColor);
		}
		if (selected) highlight();
		Glass.material.SetColor("_Color", _active ? _primaryColor : Color.black);
	}

	private void highlight() {
		Color color = _active && _primaryColor != Color.white ? negative(_primaryColor) : Color.red;
		Button.material.SetColor("_Color", color);
	}

	private Color negative(Color color) {
		return Color.white - color + Color.black;
	}
}
