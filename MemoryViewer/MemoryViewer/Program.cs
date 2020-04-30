using System;
using System.Diagnostics;
using System.Linq;
using SDL2;
using Shared;

namespace MemoryViewer
{
	class Program
	{	
		public static int Main(string[] args)
		{
			const int RESX = 320;
			const int RESY = 240;
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
			if(System.IO.File.Exists("palette.dat")) 
			{
				var palette = System.IO.File.ReadAllBytes("palette.dat");
				for(int i = 0; i < 256; i++) 
				{
					byte r = palette[i * 3 + 0];
					byte g = palette[i * 3 + 1];
					byte b = palette[i * 3 + 2];
					pal256[i] = (uint)(r << 16 | g << 8 | b);				
				}
			}
			else
			{
				for(int i = 0; i < 256; i++) 
				{
					pal256[i] = (uint)(i << 16 | i << 8 | i);				
				}
			}			
				
			bool quit = false;
			uint[] pixels = new uint[RESX * RESY * 11];
			byte[] pixelData = new byte[(1024+256) * 1024];
			byte[] oldPixelData = new byte[(1024+256) * 1024];
			
			int offset = 0, lastOffset = -1;	
			ProcessMemoryReader memoryReader = null;
			long memoryAddress = -1;
			int mousePosition = 0, lastMousePosition = -1;
			uint lastCheck = 0;
			
			float scrollVel = 0.0f, scrollPos = 0.0f;
			int scrollStartPos = 0;
			bool startScroll = false;
			
			for(int i = (640+64)*1024 ; i < pixels.Length ; i++)
			{
				pixels[i] = 0xff080808;
			}
										
			Action updatePos = () => 
			{		
				if(offset != lastOffset || lastMousePosition != mousePosition)
				{
					offset = Math.Max(offset, 0);				
					offset = Math.Min(offset, pixelData.Length);
					
					int mousePos = mousePosition + offset;
					if(mousePos >= 640*1024) mousePos += (384+192)*1024;
					SDL.SDL_SetWindowTitle(window, string.Format("{0:X6} - {1:X6}", offset, mousePos));
					
					lastOffset = offset;
					lastMousePosition = mousePosition;
				}
			};
						
			uint currentTime = SDL.SDL_GetTicks();
			uint previousTime = currentTime;
			
			while(!quit)
			{
				previousTime = currentTime;
				currentTime = SDL.SDL_GetTicks();
				float deltaTime = (currentTime - previousTime) / 1000.0f;
				
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
							
							int tmx = Math.Max(winx / RESX, 1); 
							float sizex =  winx / (float)tmx;
							int page = (int)Math.Floor(x / sizex);
														
							mousePosition = page * RESX * winy + (int)Math.Floor(x / sizex * RESX) + y * RESX;							
							break;
							
						case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
							scrollStartPos = GetMouseYPosition();
							startScroll = true;
							break;
																
						case SDL.SDL_EventType.SDL_MOUSEWHEEL:
							scrollVel -= sdlEvent.wheel.y * 500;							
							break;							
							
						case SDL.SDL_EventType.SDL_KEYDOWN:
							switch(sdlEvent.key.keysym.sym)
							{								
								case SDL.SDL_Keycode.SDLK_DOWN:
									offset += RESX;
									break;

								case SDL.SDL_Keycode.SDLK_UP:
									offset -= RESX;	
									break;
									
								case SDL.SDL_Keycode.SDLK_LEFT:
									offset++;
								break;

								case SDL.SDL_Keycode.SDLK_RIGHT:
									offset--;
									break;
													
								case SDL.SDL_Keycode.SDLK_PAGEDOWN:
									offset += winx * winy;
									break;

								case SDL.SDL_Keycode.SDLK_PAGEUP:
									offset -= winx * winy;
									break;
							}
							break;
					}
				}	
								
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
					long result = memoryReader.Read(pixelData, memoryAddress, pixelData.Length);
					if(result == 0 || result != pixelData.Length)
					{						
						memoryReader.Close();
						memoryReader = null;
					}
					else
					{
						Array.Copy(pixelData, (1024+192)*1024, pixelData, 640*1024, 64*1024);
					}
				}
				
				if(startScroll)
				{
					if(!GetMouseState())
					{
						startScroll = false;
					}
					else
					{
						int mousePos = GetMouseYPosition();
						scrollVel = (scrollStartPos - GetMouseYPosition()) / deltaTime;
						scrollStartPos = mousePos;
					}
				}				

				//scroll
				scrollPos += scrollVel * deltaTime;
				scrollVel *= (float)Math.Pow(0.008f, deltaTime);
				int delta = (int)scrollPos;
				if (delta <= -1 || delta >= 1)
				{
					offset += delta * RESX;
					scrollPos -= delta;
				}			
				else
				{
					scrollVel = 0.0f;
				}
				
				updatePos();
				
				unsafe
				{
					fixed (byte* pixelsBytePtr = pixelData)
					fixed (byte* oldPixelsBytePtr = oldPixelData)
					{
						ulong* pixelsPtr = (ulong*)pixelsBytePtr;
						ulong* oldPixelsPtr = (ulong*)oldPixelsBytePtr;
						
						for(int i = 0 ; i < (640+64)*1024 ; i += 8)
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
			
				SDL.SDL_SetRenderDrawColor(renderer, 8, 8, 8, 255);
				SDL.SDL_RenderClear(renderer);
								
				int skip = 0;
				int tm = Math.Max(winx / RESX, 1);
				int tn = (int)Math.Ceiling(winy / (float)RESY);
				float size =  winx / (float)tm;
				
				for(int m = 0 ; m < tm ; m++)
				{
					for(int n = 0 ; n < tn ; n++)
					{					
						int position = offset + skip + n * RESX * RESY;
						if (position >= (640+64)*1024) continue;
												
						unsafe
						{
							fixed (uint* pixelsBuf = &pixels[position])
							{
								SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
							}
						}
						
						drawRect.x = (int)Math.Round(m * size);
						drawRect.y = n * RESY;
						drawRect.w = (int)Math.Round((m+1) * size) - drawRect.x;
						SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
					}
					
					skip += RESX * winy;
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
		
		public static int GetMouseYPosition()
		{
			int x, y;
			SDL.SDL_GetGlobalMouseState(out x, out y);
			return y;
		}
		
		public static bool GetMouseState()
		{
			int x, y;
			return SDL.SDL_GetGlobalMouseState(out x, out y) != 0;
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