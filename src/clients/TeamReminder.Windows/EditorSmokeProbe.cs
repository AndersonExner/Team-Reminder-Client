#if DEBUG
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using TeamReminder.Core;
using TeamReminder.Local;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
namespace TeamReminder.Windows;
internal static class EditorSmokeProbe
{
    public static async Task RunAsync(MainWindow window, AlarmsViewModel vm, LocalSoundLibrary sounds, string reportPath)
    {
        var groupId=Guid.NewGuid();
        vm.SetDestinations([new(groupId,"Desenvolvimento DigitalDesk","",Guid.NewGuid(),GroupRole.Administrator,1,4)]);
        vm.BeginCreate();vm.Title="Reunião do desenvolvimento";
        var dialog=new AlarmEditorDialog(vm,sounds,WinRT.Interop.WindowNative.GetWindowHandle(window)){
            XamlRoot=((FrameworkElement)window.Content).XamlRoot,
            RequestedTheme=((FrameworkElement)window.Content).RequestedTheme
        };
        dialog.Opened+=async(_,_)=>{
            try{
                vm.Destination=vm.Destinations[1];await Task.Delay(300);
                var content=(FrameworkElement)dialog.Content;
                var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync(content);
                var buffer=await bitmap.GetPixelsAsync();var pixels=new byte[buffer.Length];
                using(var reader=DataReader.FromBuffer(buffer))reader.ReadBytes(pixels);
                var imagePath=Path.ChangeExtension(reportPath,"png");await File.WriteAllBytesAsync(imagePath,[]);
                var file=await StorageFile.GetFileFromPathAsync(Path.GetFullPath(imagePath));
                using(var stream=await file.OpenAsync(FileAccessMode.ReadWrite)){
                    var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels);
                    await encoder.FlushAsync();
                }
                await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new{
                    Success=dialog.PrimaryButtonText=="Publicar no grupo",
                    dialog.PrimaryButtonText,Width=content.ActualWidth,Height=content.ActualHeight,
                    vm.DestinationHint,Image=imagePath
                }));
            }catch(Exception ex){await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new{Success=false,Error=ex.ToString()}));}
            finally{dialog.Hide();}
        };
        try{await dialog.ShowAsync();}finally{dialog.DisposeSoundEditor();}
    }
}
#endif
