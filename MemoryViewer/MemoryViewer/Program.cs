﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SDL2;
using Shared;

namespace MemoryViewer
{
	class Program
	{
		const int RESX = 320;
		const int RESY = 240;
		static int winx, winy, zoom;
			
		public static int Main(string[] args)
		{
			bool mcb = false;

			winx = Tools.GetArgument(args, "-screen-width") ?? 640;
			winy = Tools.GetArgument(args, "-screen-height") ?? 480;
			zoom = Tools.GetArgument(args, "-zoom") ?? 2;

			//init SDL
			SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

			IntPtr window = SDL.SDL_CreateWindow("AITD memory viewer", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, winx, winy, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
			IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
			//IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);

			//SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "linear");
			IntPtr texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, RESX, RESY);
			
			uint[] palette = LoadPalette();

			bool quit = false;
			uint[] pixels = new uint[RESX * RESY * 10];
			uint[] mcbPixels = new uint[RESX * RESY * 10];			
			byte[] pixelData = new byte[640 * 1024 + 64000];
			byte[] oldPixelData = new byte[640 * 1024 + 64000];			

			ProcessMemoryReader processReader = null;
			long memoryAddress = -1;
			uint lastCheck = 0;

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

						case SDL.SDL_EventType.SDL_KEYDOWN:
							if(sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_SPACE)
							{
								mcb = !mcb;
							}
							break;

						case SDL.SDL_EventType.SDL_WINDOWEVENT:

							if(sdlEvent.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
							{
								winx = sdlEvent.window.data1;
								winy = sdlEvent.window.data2;
							}
							break;
					}
				}

				if (processReader == null)
				{
					uint time = SDL.SDL_GetTicks();
					if ((time - lastCheck) > 1000 || lastCheck == 0)
					{
						lastCheck = time;

						int processId = DosBox.SearchProcess();
						if (processId != -1)
						{
							processReader = new ProcessMemoryReader(processId);
							memoryAddress = processReader.SearchFor16MRegion();
							if(memoryAddress == -1)
							{
								processReader.Close();
								processReader = null;
							}
						}
					}
				}

				if (processReader != null)
				{
					//EMS memory (64000B) (skip 64KB (HMA) + 128KB (VCPI))
					//DOS conventional memory (640KB)
					if(!(processReader.Read(pixelData, memoryAddress+(1024+192)*1024, 64000) > 0 &&
						(processReader.Read(pixelData, memoryAddress, 640 * 1024, 64000) > 0)))
					{
						processReader.Close();
						processReader = null;
					}
				}

				Update(pixelData, oldPixelData, pixels, palette);
				UpdateMCB(pixelData, oldPixelData, 64000, mcbPixels);

				//render
				SDL.SDL_RenderClear(renderer);

				int tm = (winx + RESX * zoom - 1) / (RESX * zoom);
				int tn = (winy + RESY * zoom - 1) / (RESY * zoom);
				
				SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_NONE);
				Render(renderer, texture, tm, tn, pixels);
				
				if (mcb)
				{
					SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
					Render(renderer, texture, tm, tn, mcbPixels);
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
		
		static unsafe void Update(byte[] pixelData, byte[] oldPixelData, uint[] pixels, uint[] palette)
		{
			//compare current vs old and only update pixels that have changed
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
							pixels[j] = palette[pixelData[j]];
						}
					}

					pixelsPtr++;
					oldPixelsPtr++;
				}
			}
		}
		
		static void UpdateMCB(byte[] pixelData, byte[] oldPixelData, int offset, uint[] pixels)
		{		
			if (!DosBox.GetMCBs(pixelData, offset).SequenceEqual(DosBox.GetMCBs(oldPixelData, offset)))
			{	
				int psp = pixelData.ReadUnsignedShort(0x0B30 + offset) * 16;
				
				bool inverse = true;
				foreach (var block in DosBox.GetMCBs(pixelData, offset))
				{
					int dest = block.Position - 16 + offset;
					int length = Math.Min(block.Size + 16, pixels.Length - dest);
	
					uint color = inverse ? 0x900080F0 : 0x902000A0; //used
					if (block.Position == psp) color = 0x90800000; //current executable
					if (block.Owner != psp && block.Owner != 0) color = 0x90808000;
					if (block.Owner == 0) color = 0x90008000; //free
	
					for (int i = 0 ; i < length ; i++)
					{
						pixels[dest++] = color;
					}
					
					inverse = !inverse;
				}
			}
		}
		
		static unsafe void Render(IntPtr renderer, IntPtr texture, int tm, int tn, uint[] pixels)
		{
			SDL.SDL_Rect textureRect = new SDL.SDL_Rect 
			{ 
				x = 0, 
				y = 0, 
				w = RESX, 
				h = RESY 
			};
			
			int skip = 0;
			for (int m = 0 ; m < tm ; m++)
			{
				for (int n = 0 ; n < tn ; n++)
				{
					int position = skip + n * RESX * RESY;
					if ((position + RESY * RESY) > pixels.Length) continue;

					fixed (uint* pixelsBuf = &pixels[position])
					{
						SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
					}

					SDL.SDL_Rect drawRect = new SDL.SDL_Rect 
					{
						x = m * RESX * zoom, 
						y = n * RESY * zoom, 
						w = RESX * zoom, 
						h = RESY * zoom 
					};
					
					SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
				}

				skip += RESX * (winy / zoom);
			}
		}
		
		static uint[] LoadPalette()
		{
			var palette = new uint[256];
			using (var stream = Assembly.GetExecutingAssembly()
				   .GetManifestResourceStream("MemoryViewer.palette.dat"))
			using (BinaryReader br = new BinaryReader(stream))
			{
				var buffer = br.ReadBytes(768);
				for(int i = 0; i < palette.Length; i++)
				{
					byte r = buffer[i * 3 + 0];
					byte g = buffer[i * 3 + 1];
					byte b = buffer[i * 3 + 2];
					palette[i] = (uint)(r << 16 | g << 8 | b);
				}
			}
			
			return palette;
		}
	}
}