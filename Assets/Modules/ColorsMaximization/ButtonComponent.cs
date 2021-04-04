using UnityEngine;

public class ButtonComponent : MonoBehaviour {
	public static readonly Color DEFAULT_BUTTON_COLOR = new Color(.33f, .33f, .33f);

	public TextMesh textMesh;
	public Renderer glass;
	public Renderer button;
	public Shader selectedShader;
	public Shader unselectedShader;

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
		KMSelectable selfSelectable = GetComponent<KMSelectable>();
		selfSelectable.OnHighlight += () => {
			selected = true;
			button.material.shader = selectedShader;
			highlight();
		};
		selfSelectable.OnHighlightEnded += () => {
			selected = false;
			button.material.shader = unselectedShader;
			button.material.SetColor("_Color", DEFAULT_BUTTON_COLOR);
		};
	}

	private void OnChanged() {
		if (!_colorblindMode || !_active) textMesh.text = "";
		else {
			textMesh.text = ColorsMaximizationModule.colorsName[_primaryColor].Substring(0, 1);
			textMesh.color = negative(_primaryColor);
		}
		if (selected) highlight();
		glass.material.SetColor("_Color", _active ? _primaryColor : Color.black);
	}

	private void highlight() {
		Color color = _active && _primaryColor != Color.white ? negative(_primaryColor) : Color.red;
		button.material.SetColor("_Color", color);
	}

	private Color negative(Color color) {
		return Color.white - color + Color.black;
	}
}
