using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SchemaViz.Gui.ViewModels.Diagram;

namespace SchemaViz.Gui.Views;

public partial class SchemaDiagramView : UserControl
{
	private const double ZoomStepFactor = 0.1;

	private TableNodeViewModel? _draggingNode;
	private Point _dragStartPointer;
	private double _dragStartX;
	private double _dragStartY;

	private bool _isPanning;
	private Point _panStartPointer;
	private double _panStartOffsetX;
	private double _panStartOffsetY;

	private double _lastZoom = 1.0;
	private double _lastOffsetX = 0.0;
	private double _lastOffsetY = 0.0;
	private SchemaDiagramViewModel? _attachedDiagram;
	private bool _suppressDiagramEvents;
	private bool _initialLayoutApplied;
	private bool _initialViewportCentered;

	public SchemaDiagramView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		DataContextChanged += OnDataContextChanged;
	}

	private Control? DiagramViewportElement => this.FindControl<Control>("DiagramViewport");

	private Canvas? DiagramCanvasElement => this.FindControl<Canvas>("DiagramCanvas");

	private SchemaDiagramViewModel? Diagram => DataContext as SchemaDiagramViewModel;

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
	{
		var props = e.GetCurrentPoint(this).Properties;
		if (!props.IsLeftButtonPressed)
		{
			return;
		}

		if (Diagram is null || sender is not Control control)
		{
			return;
		}

		if (control.DataContext is not TableNodeViewModel node)
		{
			return;
		}

		Diagram.SelectTable(node);
		_draggingNode = node;
		_dragStartPointer = DiagramViewportElement is { } viewport
			? e.GetPosition(viewport)
			: e.GetPosition(this);
		_dragStartX = node.X;
		_dragStartY = node.Y;
		e.Pointer.Capture(control);
		e.Handled = true;
	}

	private void OnNodePointerMoved(object? sender, PointerEventArgs e)
	{
		if (_draggingNode is null || Diagram is null)
		{
			return;
		}

		var current = DiagramViewportElement is { } viewport
			? e.GetPosition(viewport)
			: e.GetPosition(this);
		var deltaX = (current.X - _dragStartPointer.X) / Diagram.Zoom;
		var deltaY = (current.Y - _dragStartPointer.Y) / Diagram.Zoom;
		_draggingNode.X = _dragStartX + deltaX;
		_draggingNode.Y = _dragStartY + deltaY;
		UpdateCanvasSize();
		e.Handled = true;
	}

	private void OnNodePointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (_draggingNode is null)
		{
			return;
		}

		e.Pointer.Capture(null);
		_draggingNode = null;
		e.Handled = true;
	}

	private void OnNodeSizeChanged(object? sender, SizeChangedEventArgs e)
	{
		if (sender is not Control control || control.DataContext is not TableNodeViewModel node)
		{
			return;
		}

		node.Width = e.NewSize.Width;
		node.Height = e.NewSize.Height;
		UpdateCanvasSize();
		EnsureInitialLayout();
	}

	private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (Diagram is null || DiagramViewportElement is null)
		{
			return;
		}

		var props = e.GetCurrentPoint(DiagramViewportElement).Properties;
		if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
		{
			_isPanning = true;
			_panStartPointer = e.GetPosition(DiagramViewportElement);
			_panStartOffsetX = Diagram.OffsetX;
			_panStartOffsetY = Diagram.OffsetY;
			e.Pointer.Capture(DiagramViewportElement);
			e.Handled = true;
			return;
		}

		if (props.IsLeftButtonPressed)
		{
			Diagram.SelectTable(null);
		}
	}

	private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!_isPanning || Diagram is null || DiagramViewportElement is null || DiagramCanvasElement is null)
		{
			return;
		}

		var current = e.GetPosition(DiagramViewportElement);
		var delta = current - _panStartPointer;

		// With Scale then Translate, offset is in screen space, so pan directly
		var newOffsetX = _panStartOffsetX + delta.X;
		var newOffsetY = _panStartOffsetY + delta.Y;

		// Clamp to canvas bounds
		var clamped = ClampOffsetToBounds(newOffsetX, newOffsetY, Diagram.Zoom);
		Diagram.OffsetX = clamped.X;
		Diagram.OffsetY = clamped.Y;
		e.Handled = true;
	}

	private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		if (Diagram is null || DiagramViewportElement is null || DiagramCanvasElement is null)
		{
			return;
		}

		var delta = e.Delta.Y;
		if (Math.Abs(delta) < double.Epsilon)
		{
			return;
		}

		var oldZoom = Diagram.Zoom;
		if (oldZoom <= 0)
		{
			return;
		}

		var zoomChange = 1.0 + (delta * ZoomStepFactor);
		if (zoomChange < 0.1)
		{
			zoomChange = 0.1;
		}

		var newZoom = oldZoom * zoomChange;
		var minZoom = SchemaDiagramViewModel.MinZoom;
		var maxZoom = SchemaDiagramViewModel.MaxZoom;
		newZoom = Math.Clamp(newZoom, minZoom, maxZoom);

		if (Math.Abs(newZoom - oldZoom) < 0.0001)
		{
			return;
		}

		var viewportPos = e.GetPosition(DiagramViewportElement);
		var worldX = (viewportPos.X - Diagram.OffsetX) / oldZoom;
		var worldY = (viewportPos.Y - Diagram.OffsetY) / oldZoom;
		var newOffsetX = viewportPos.X - worldX * newZoom;
		var newOffsetY = viewportPos.Y - worldY * newZoom;

		var clamped = ClampOffsetToBounds(newOffsetX, newOffsetY, newZoom);

		_suppressDiagramEvents = true;
		try
		{
			Diagram.Zoom = newZoom;
			Diagram.OffsetX = clamped.X;
			Diagram.OffsetY = clamped.Y;
		}
		finally
		{
			_suppressDiagramEvents = false;
		}

		_lastZoom = newZoom;
		_lastOffsetX = clamped.X;
		_lastOffsetY = clamped.Y;
		e.Handled = true;
	}

	private Point ClampOffsetToBounds(double offsetX, double offsetY, double zoom)
	{
		if (DiagramViewportElement is null || DiagramCanvasElement is null)
		{
			return new Point(offsetX, offsetY);
		}

		var viewportSize = DiagramViewportElement.Bounds;
		var canvasWidth = DiagramCanvasElement.Width;
		var canvasHeight = DiagramCanvasElement.Height;
		var canvasScreenWidth = canvasWidth * zoom;
		var canvasScreenHeight = canvasHeight * zoom;

		// With Scale then Translate: screen = world * zoom + offset
		// Viewport top-left (screen 0,0) maps to world: (0 - offset) / zoom
		// We want: (0 - offset) / zoom >= 0, so offset <= 0
		// Viewport bottom-right maps to world: (viewportSize - offset) / zoom
		// We want: (viewportSize - offset) / zoom <= canvasSize
		// So: viewportSize - offset <= canvasSize * zoom
		// So: offset >= viewportSize - canvasSize * zoom

		// Calculate valid offset range
		var minOffsetX = viewportSize.Width - canvasScreenWidth;
		var minOffsetY = viewportSize.Height - canvasScreenHeight;
		var maxOffsetX = 0.0;
		var maxOffsetY = 0.0;

		// When canvas fits: minOffset is positive (e.g., 172), maxOffset is 0
		// Range should be [0, minOffset] to allow centering
		// When canvas doesn't fit: minOffset is negative, maxOffset is 0
		// Range is [minOffset, 0] to show canvas bounds
		if (canvasScreenWidth <= viewportSize.Width && canvasScreenHeight <= viewportSize.Height)
		{
			// Canvas fits - range is [0, minOffset] to allow centering
			var resultX = Math.Max(0.0, Math.Min(minOffsetX, offsetX));
			var resultY = Math.Max(0.0, Math.Min(minOffsetY, offsetY));
			return new Point(resultX, resultY);
		}
		else
		{
			// Canvas doesn't fit - range is [minOffset, 0]
			var resultX = Math.Max(minOffsetX, Math.Min(maxOffsetX, offsetX));
			var resultY = Math.Max(minOffsetY, Math.Min(maxOffsetY, offsetY));
			return new Point(resultX, resultY);
		}
	}

	private void UpdateCanvasSize()
	{
		if (Diagram is null)
		{
			return;
		}

		if (Diagram.Tables is null || Diagram.Tables.Count == 0)
		{
			var width = Math.Max(SchemaDiagramViewModel.MinCanvasWidth, Diagram.CanvasWidth);
			var height = Math.Max(SchemaDiagramViewModel.MinCanvasHeight, Diagram.CanvasHeight);

			_suppressDiagramEvents = true;
			try
			{
				Diagram.CanvasWidth = width;
				Diagram.CanvasHeight = height;
			}
			finally
			{
				_suppressDiagramEvents = false;
			}

			return;
		}

		double maxRight = 0;
		double maxBottom = 0;

		foreach (var table in Diagram.Tables)
		{
			var right = table.X + table.Width;
			var bottom = table.Y + table.Height;

			if (right > maxRight)
			{
				maxRight = right;
			}

			if (bottom > maxBottom)
			{
				maxBottom = bottom;
			}
		}

		const double padding = 200;
		var targetWidth = Math.Max(SchemaDiagramViewModel.MinCanvasWidth, maxRight + padding);
		var targetHeight = Math.Max(SchemaDiagramViewModel.MinCanvasHeight, maxBottom + padding);

		_suppressDiagramEvents = true;
		try
		{
			Diagram.CanvasWidth = targetWidth;
			Diagram.CanvasHeight = targetHeight;
		}
		finally
		{
			_suppressDiagramEvents = false;
		}
	}

	private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (_isPanning)
		{
			e.Pointer.Capture(null);
			_isPanning = false;
			e.Handled = true;
		}
	}

	private void OnLoaded(object? sender, RoutedEventArgs e)
	{
		if (Diagram is null)
		{
			return;
		}

		AttachToDiagram(Diagram);

		// Initialize view to center the canvas
		InitializeViewToCenter();
	}

	private void InitializeViewToCenter()
	{
		if (Diagram is null || DiagramViewportElement is null || DiagramCanvasElement is null)
		{
			return;
		}

		// Wait for layout to complete
		DiagramViewportElement.LayoutUpdated += OnViewportLayoutUpdated;
	}


	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		AttachToDiagram(Diagram);
	}

	private void AttachToDiagram(SchemaDiagramViewModel? diagram)
	{
		if (_attachedDiagram is not null)
		{
			_attachedDiagram.PropertyChanged -= OnDiagramPropertyChanged;
			_attachedDiagram.SchemaExportRequested -= OnSchemaExportRequested;
			_attachedDiagram = null;
		}

		if (diagram is null)
		{
			return;
		}

		_attachedDiagram = diagram;
		_lastZoom = diagram.Zoom;
		_lastOffsetX = diagram.OffsetX;
		_lastOffsetY = diagram.OffsetY;
		diagram.PropertyChanged += OnDiagramPropertyChanged;
		diagram.SchemaExportRequested += OnSchemaExportRequested;
		_initialLayoutApplied = false;
		_initialViewportCentered = false;
		UpdateCanvasSize();
		EnsureInitialLayout();
	}

	private void CenterViewportOnTables(bool force = false)
	{
		if (Diagram is null || DiagramViewportElement is null || DiagramCanvasElement is null)
		{
			return;
		}

		if (!force && _initialViewportCentered)
		{
			return;
		}

		if (Diagram.Tables.Count == 0)
		{
			return;
		}

		if (Diagram.Tables.Any(table => table.Width <= 0 || table.Height <= 0))
		{
			return;
		}

		var viewportBounds = DiagramViewportElement.Bounds;
		if (viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
		{
			return;
		}

		var zoom = Diagram.Zoom;
		var minX = Diagram.Tables.Min(table => table.X);
		var minY = Diagram.Tables.Min(table => table.Y);
		var maxX = Diagram.Tables.Max(table => table.X + table.Width);
		var maxY = Diagram.Tables.Max(table => table.Y + table.Height);

		var worldCenterX = (minX + maxX) / 2.0;
		var worldCenterY = (minY + maxY) / 2.0;
		var viewportCenterX = viewportBounds.Width / 2.0;
		var viewportCenterY = viewportBounds.Height / 2.0;

		var desiredOffsetX = viewportCenterX - worldCenterX * zoom;
		var desiredOffsetY = viewportCenterY - worldCenterY * zoom;
		var clamped = ClampOffsetToBounds(desiredOffsetX, desiredOffsetY, zoom);

		_suppressDiagramEvents = true;
		try
		{
			Diagram.OffsetX = clamped.X;
			Diagram.OffsetY = clamped.Y;
		}
		finally
		{
			_suppressDiagramEvents = false;
		}

		_lastOffsetX = Diagram.OffsetX;
		_lastOffsetY = Diagram.OffsetY;
		_initialViewportCentered = true;
	}

	private void EnsureInitialLayout()
	{
		if (_initialLayoutApplied || Diagram is null)
		{
			return;
		}

		if (Diagram.Tables.Count == 0)
		{
			return;
		}

		if (Diagram.Tables.Any(table => table.Width <= 0 || table.Height <= 0))
		{
			return;
		}

		Diagram.ApplyAutoLayout();
		UpdateCanvasSize();
		CenterViewportOnTables(force: true);
		_initialLayoutApplied = true;
	}

	private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_suppressDiagramEvents)
		{
			return;
		}

		if (!ReferenceEquals(sender, _attachedDiagram) || DiagramViewportElement is null)
		{
			return;
		}

		if (e.PropertyName == nameof(SchemaDiagramViewModel.Zoom))
		{
			var diagram = _attachedDiagram!;
			var newZoom = diagram.Zoom;
			var oldZoom = _lastZoom;
			if (Math.Abs(newZoom - oldZoom) < 0.0001 ||
				oldZoom <= 0 ||
				DiagramViewportElement is null ||
				DiagramCanvasElement is null)
			{
				_lastZoom = newZoom;
				_lastOffsetX = diagram.OffsetX;
				_lastOffsetY = diagram.OffsetY;
				return;
			}

			var viewportSize = DiagramViewportElement.Bounds;
			if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
			{
				_lastZoom = newZoom;
				_lastOffsetX = diagram.OffsetX;
				_lastOffsetY = diagram.OffsetY;
				return;
			}

			var canvasWidth = DiagramCanvasElement.Width;
			var canvasHeight = DiagramCanvasElement.Height;
			var canvasScreenWidth = canvasWidth * newZoom;
			var canvasScreenHeight = canvasHeight * newZoom;
			var fitsInViewport =
				canvasScreenWidth <= viewportSize.Width &&
				canvasScreenHeight <= viewportSize.Height;

			// Check if at minimum zoom - if so, always center the canvas
			var isAtMinimumZoom = newZoom <= 0.16;
			double newOffsetX;
			double newOffsetY;

			if (isAtMinimumZoom && fitsInViewport)
			{
				// At minimum zoom and canvas fits - center it in viewport
				newOffsetX = (viewportSize.Width - canvasScreenWidth) / 2.0;
				newOffsetY = (viewportSize.Height - canvasScreenHeight) / 2.0;
			}
			else
			{
				// Normal zoom-to-center: keep the world point at viewport center fixed
				// Transform: screen = world * zoom + offset
				// So: world = (screen - offset) / zoom
				var viewportCenterX = viewportSize.Width / 2.0;
				var viewportCenterY = viewportSize.Height / 2.0;

				// Get the world coordinate currently at the viewport center
				var worldX = (viewportCenterX - _lastOffsetX) / oldZoom;
				var worldY = (viewportCenterY - _lastOffsetY) / oldZoom;

				// Calculate new offset to keep that same world point at the center
				newOffsetX = viewportCenterX - worldX * newZoom;
				newOffsetY = viewportCenterY - worldY * newZoom;
			}

			// Clamp to canvas bounds
			var clamped = ClampOffsetToBounds(newOffsetX, newOffsetY, newZoom);

			// Update offset
			_suppressDiagramEvents = true;
			try
			{
				diagram.OffsetX = clamped.X;
				diagram.OffsetY = clamped.Y;
			}
			finally
			{
				_suppressDiagramEvents = false;
			}

			_lastZoom = newZoom;
			_lastOffsetX = clamped.X;
			_lastOffsetY = clamped.Y;
		}
		else if (e.PropertyName == nameof(SchemaDiagramViewModel.OffsetX) ||
				 e.PropertyName == nameof(SchemaDiagramViewModel.OffsetY))
		{
			// Clamp offset to bounds when it changes (from panning or manual setting)
			if (_attachedDiagram is not null &&
				DiagramViewportElement is not null &&
				DiagramCanvasElement is not null)
			{
				var clamped = ClampOffsetToBounds(
					_attachedDiagram.OffsetX,
					_attachedDiagram.OffsetY,
					_attachedDiagram.Zoom);

				if (Math.Abs(clamped.X - _attachedDiagram.OffsetX) > 0.01 ||
					Math.Abs(clamped.Y - _attachedDiagram.OffsetY) > 0.01)
				{
					// Offset was out of bounds, clamp it
					_suppressDiagramEvents = true;
					try
					{
						_attachedDiagram.OffsetX = clamped.X;
						_attachedDiagram.OffsetY = clamped.Y;
					}
					finally
					{
						_suppressDiagramEvents = false;
					}
				}

				// Track offset changes so we have accurate values for zoom calculations
				_lastOffsetX = _attachedDiagram.OffsetX;
				_lastOffsetY = _attachedDiagram.OffsetY;
			}
		}
	}

	private void OnViewportLayoutUpdated(object? sender, EventArgs e)
	{
		if (DiagramViewportElement is null)
		{
			return;
		}

		// Only initialize once
		DiagramViewportElement.LayoutUpdated -= OnViewportLayoutUpdated;
		CenterViewportOnTables();
	}

	private async void OnSchemaExportRequested(object? sender, SchemaExportRequestedEventArgs e)
	{
		if (TopLevel.GetTopLevel(this) is not Window window)
		{
			return;
		}

		var storageProvider = window.StorageProvider;
		if (storageProvider is null || !storageProvider.CanSave)
		{
			return;
		}

		var defaultName = string.IsNullOrWhiteSpace(e.SuggestedFileName)
			? "schema-export"
			: e.SuggestedFileName;
		var initialFileName = defaultName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
			? defaultName
			: $"{defaultName}.json";

		var downloadsFolder = GetWellKnownFolderPath(Environment.SpecialFolder.UserProfile, "Downloads");
		var suggestedFolder = await TryGetFolderAsync(storageProvider, downloadsFolder);

		var options = new FilePickerSaveOptions
		{
			Title = "Export schema as JSON",
			SuggestedFileName = initialFileName,
			DefaultExtension = "json",
			ShowOverwritePrompt = true,
			SuggestedStartLocation = suggestedFolder,
			FileTypeChoices = new List<FilePickerFileType>
			{
				new("JSON Files") { Patterns = new[] { "*.json" } },
				new("All Files") { Patterns = new[] { "*.*" } }
			}
		};

		var file = await storageProvider.SaveFilePickerAsync(options);
		if (file is null)
		{
			return;
		}

		try
		{
			await using var stream = await file.OpenWriteAsync();
			await using var writer = new StreamWriter(stream);
			await writer.WriteAsync(e.JsonContent);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to export schema: {ex.Message}");
		}
	}

	private Window? GetWindow()
	{
		return TopLevel.GetTopLevel(this) as Window;
	}

	private async void OnExportViewPngClick(object? sender, RoutedEventArgs e)
	{
		var window = GetWindow();
		if (window is null)
		{
			return;
		}

		var storageProvider = window.StorageProvider;
		if (storageProvider is null || !storageProvider.CanSave)
		{
			return;
		}

		var picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
		var suggestedFolder = await TryGetFolderAsync(storageProvider, picturesFolder);

		var options = new FilePickerSaveOptions
		{
			Title = "Export diagram (current view) as PNG",
			SuggestedFileName = "schema-view.png",
			DefaultExtension = "png",
			ShowOverwritePrompt = true,
			SuggestedStartLocation = suggestedFolder,
			FileTypeChoices = new List<FilePickerFileType>
			{
				new("PNG image") { Patterns = new[] { "*.png" } },
				new("All Files") { Patterns = new[] { "*.*" } }
			}
		};

		var file = await storageProvider.SaveFilePickerAsync(options);
		if (file is null)
		{
			return;
		}

		try
		{
			var filePath = file.Path.LocalPath;
			await ExportViewportToPngAsync(filePath);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to export viewport PNG: {ex.Message}");
		}
	}

	private async void OnExportFullPngClick(object? sender, RoutedEventArgs e)
	{
		var window = GetWindow();
		if (window is null)
		{
			return;
		}

		var storageProvider = window.StorageProvider;
		if (storageProvider is null || !storageProvider.CanSave)
		{
			return;
		}

		var picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
		var suggestedFolder = await TryGetFolderAsync(storageProvider, picturesFolder);

		var options = new FilePickerSaveOptions
		{
			Title = "Export full diagram as PNG",
			SuggestedFileName = "schema-full.png",
			DefaultExtension = "png",
			ShowOverwritePrompt = true,
			SuggestedStartLocation = suggestedFolder,
			FileTypeChoices = new List<FilePickerFileType>
			{
				new("PNG image") { Patterns = new[] { "*.png" } },
				new("All Files") { Patterns = new[] { "*.*" } }
			}
		};

		var file = await storageProvider.SaveFilePickerAsync(options);
		if (file is null)
		{
			return;
		}

		try
		{
			var filePath = file.Path.LocalPath;
			await ExportFullDiagramToPngAsync(filePath);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to export full diagram PNG: {ex.Message}");
		}
	}

	private static string? GetWellKnownFolderPath(Environment.SpecialFolder baseFolder, string childFolderName)
	{
		try
		{
			var basePath = Environment.GetFolderPath(baseFolder);
			if (string.IsNullOrWhiteSpace(basePath))
			{
				return null;
			}

			var combined = Path.Combine(basePath, childFolderName);
			return combined;
		}
		catch
		{
			return null;
		}
	}

	private static async Task<IStorageFolder?> TryGetFolderAsync(IStorageProvider provider, string? folderPath)
	{
		if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
		{
			return null;
		}

		try
		{
			var uri = new Uri(folderPath);
			return await provider.TryGetFolderFromPathAsync(uri);
		}
		catch
		{
			return null;
		}
	}

	private async Task ExportViewportToPngAsync(string filePath)
	{
		if (DiagramViewportElement is null)
		{
			return;
		}

		// Ensure layout is up-to-date
		DiagramViewportElement.Measure(Size.Infinity);
		DiagramViewportElement.Arrange(DiagramViewportElement.Bounds);
		DiagramViewportElement.UpdateLayout();

		var bounds = DiagramViewportElement.Bounds;
		if (bounds.Width <= 0 || bounds.Height <= 0)
		{
			return;
		}

		var pixelWidth = (int)Math.Ceiling(bounds.Width);
		var pixelHeight = (int)Math.Ceiling(bounds.Height);

		var rtb = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight));
		rtb.Render(DiagramViewportElement);

		await using var fs = File.Open(filePath, FileMode.Create, FileAccess.Write);
		rtb.Save(fs);
	}

	private async Task ExportFullDiagramToPngAsync(string filePath)
	{
		if (DiagramCanvasElement is null || Diagram is null)
		{
			return;
		}

		// Save current camera state
		var oldZoom = Diagram.Zoom;
		var oldOffsetX = Diagram.OffsetX;
		var oldOffsetY = Diagram.OffsetY;

		try
		{
			// Temporarily reset view so the entire canvas is in world space with 1:1 scale.
			_suppressDiagramEvents = true;
			Diagram.Zoom = 1.0;
			Diagram.OffsetX = 0.0;
			Diagram.OffsetY = 0.0;

			// Ensure layout is up-to-date
			DiagramCanvasElement.Measure(Size.Infinity);
			DiagramCanvasElement.Arrange(new Rect(0, 0, Diagram.CanvasWidth, Diagram.CanvasHeight));
			DiagramCanvasElement.UpdateLayout();

			var pixelWidth = (int)Math.Ceiling(Diagram.CanvasWidth);
			var pixelHeight = (int)Math.Ceiling(Diagram.CanvasHeight);

			if (pixelWidth <= 0 || pixelHeight <= 0)
			{
				return;
			}

			var rtb = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight));
			rtb.Render(DiagramCanvasElement);

			await using var fs = File.Open(filePath, FileMode.Create, FileAccess.Write);
			rtb.Save(fs);
		}
		finally
		{
			// Restore camera state
			_suppressDiagramEvents = true;
			try
			{
				Diagram.Zoom = oldZoom;
				Diagram.OffsetX = oldOffsetX;
				Diagram.OffsetY = oldOffsetY;
			}
			finally
			{
				_suppressDiagramEvents = false;
			}
		}
	}
}