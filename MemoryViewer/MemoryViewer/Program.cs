using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using SDL2;
using Shared;

namespace MemoryViewer
{
	class Program
	{	
		public static int Main(string[] args)
		{
			const int RESX = 320;
			const int RESY = 760;
			const int COLS = 3;
			
			int winx = GetArgument(args, "-screen-width") ?? 320;
			int winy = GetArgument(args, "-screen-height") ?? 240;
			
			//init SDL
			SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
			
			IntPtr window = SDL.SDL_CreateWindow(string.Empty, SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, winx, winy, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
			IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
			//IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
			
			SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "linear");
			SDL.SDL_Rect textureRect = new SDL.SDL_Rect { x = 0, y = 0, w = RESX, h = RESY };
			IntPtr texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, RESX, RESY);
			 
			SDL.SDL_Rect drawRect = new SDL.SDL_Rect { x = 0, y = 0, w = RESX, h = RESY };
				
			uint[] pal256 = new uint[256];
			using (var stream = Assembly.GetExecutingAssembly()
			       .GetManifestResourceStream("MemoryViewer.palette.dat"))
			using (BinaryReader br = new BinaryReader(stream))
			{				
				var palette = br.ReadBytes(768);
				for(int i = 0; i < 256; i++) 
				{
					byte r = palette[i * 3 + 0];
					byte g = palette[i * 3 + 1];
					byte b = palette[i * 3 + 2];
					pal256[i] = (uint)(r << 16 | g << 8 | b);				
				}
			}
				
			bool quit = false;
			uint[] pixels = new uint[RESX * RESY * 11];
			byte[] pixelData = new byte[640 * 1024 + 64000];
			byte[] oldPixelData = new byte[640 * 1024 + 64000];
			
			ProcessMemoryReader memoryReader = null;
			long memoryAddress = -1;
			int mousePosition = 0, lastMousePosition = -1;
			uint lastCheck = 0;
														
			Action updateWindowTitle = () => 
			{		
				if(lastMousePosition != mousePosition)
				{
					int mousePos = mousePosition;
					if(mousePos >= 640*1024) mousePos += 384*1024; //skip UMA
					SDL.SDL_SetWindowTitle(window, string.Format("{0:X6}", mousePos));
					
					lastMousePosition = mousePosition;
				}
			};
									
			while(!quit)
			{
				SDL.SDL_Event sdlEvent;				
				while(SDL.SDL_PollEvent(out sdlEvent) != 0 && !quit)
				{
					switch(sdlEvent.type)
					{
						case SDL.SDL_EventType.SDL_QUIT:
							quit = true;
							break;
							
						case SDL.SDL_EventType.SDL_WINDOWEVENT:
							
							if(sdlEvent.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
							{
								winx = sdlEvent.window.data1;
								winy = sdlEvent.window.data2;
							}							
							break;
							
						case SDL.SDL_EventType.SDL_MOUSEMOTION:	
							int x = sdlEvent.motion.x;
							int y = sdlEvent.motion.y;
							
							int msizex = winx / COLS;
							int msizey = (winy * COLS * (RESX * RESY)) / pixelData.Length;
											
							int page = x / msizex;
							int pageSize = RESX * ((RESY * winy) / msizey);
														
							mousePosition = page * pageSize + ((x * RESX) / msizex) % RESX + ((y * RESY) / msizey) * RESX;
							break;									
																				
					}
				}	
				
				updateWindowTitle();
								
				if(memoryReader == null)
				{
					uint time = SDL.SDL_GetTicks();
					if ((time - lastCheck) > 1000)
					{
						lastCheck = time;					
						int[] processIds = Process.GetProcesses()
						.Where(x =>
							{
								string name;
								try
								{
									name = x.ProcessName;
								}
								catch
								{
									name = string.Empty;
								}
								return name.StartsWith("DOSBOX", StringComparison.InvariantCultureIgnoreCase);
							})
						.Select(x => x.Id)
						.ToArray();
				
						if (processIds.Any())
						{
							memoryReader = new ProcessMemoryReader(processIds.First());								
							memoryAddress = memoryReader.SearchFor16MRegion();			
							if(memoryAddress == -1)
							{
								memoryReader = null;
							}							
						}			
					}							
				}	
				
				if(memoryReader != null)
				{
					//DOS conventional memory (640KB)
					//EMS memory (64000B) (skip 64KB (HMA) + 128KB (VCPI) + 32B)
					if(memoryReader.Read(pixelData, memoryAddress, 640*1024) == 0 || 
					   memoryReader.Read(pixelData, memoryAddress+(1024+192)*1024+32, 64000, 640*1024) == 0) 
					{						
						memoryReader.Close();
						memoryReader = null;
					}
				}
				
				if(memoryReader != null)
				{				
					unsafe
					{
						fixed (byte* pixelsBytePtr = pixelData)
						fixed (byte* oldPixelsBytePtr = oldPixelData)
						{
							ulong* pixelsPtr = (ulong*)pixelsBytePtr;
							ulong* oldPixelsPtr = (ulong*)oldPixelsBytePtr;
							
							for(int i = 0 ; i < pixelData.Length ; i += 8)
							{
								if(*pixelsPtr != *oldPixelsPtr)
								{
									for(int j = i ; j < i + 8 ; j++)
									{
										pixels[j] = pal256[pixelData[j]];
									}
								}	
							
								pixelsPtr++;
								oldPixelsPtr++;
							}
						}
					}						
				}
					
				SDL.SDL_SetRenderDrawColor(renderer, 8, 8, 8, 255);
				SDL.SDL_RenderClear(renderer);
								
				int sizex = winx / COLS;
				int sizey = (winy * COLS * (RESX * RESY)) / pixelData.Length;
				int tn = (winy + sizey - 1) / sizey;
				
				int skip = 0;				
				for(int m = 0 ; m < COLS ; m++)
				{
					for(int n = 0 ; n < tn ; n++)
					{					
						int position = skip + n * RESX * RESY;
						if (position >= pixelData.Length) continue;
												
						unsafe
						{
							fixed (uint* pixelsBuf = &pixels[position])
							{
								SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
							}
						}
						
						drawRect.x = m * sizex;
						drawRect.y = n * sizey;
						drawRect.w = sizex;
						drawRect.h = sizey;
						SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
					}
					
					skip += RESX * ((RESY * winy) / sizey);
				}
				
				SDL.SDL_RenderPresent(renderer);
				
				//swap buffers
				var tmp = pixelData;
				pixelData = oldPixelData;
				oldPixelData = tmp;
			}
			
			SDL.SDL_DestroyTexture(texture);
			SDL.SDL_DestroyRenderer(renderer);
			SDL.SDL_DestroyWindow(window);
			SDL.SDL_Quit();
			
			return 0;
		}
		
		public static int? GetArgument(string[] args, string name)
		{
			int index = Array.IndexOf(args, name);
			if (index >= 0 && index < (args.Length - 1))
			{
				int value;
				if(int.TryParse(args[index + 1], out value))
				{
					return value;
				}
			}
			
			return null;
		}		
	}	
}