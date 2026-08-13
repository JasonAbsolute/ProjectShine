This is a pack of alphabet textures for the purpose of editing the "SHINE!" animation, as well as "TOO BAD!", "READY", and "GO!".

The game originally only had a limited set of characters, because not every letter in the alphabet was used.
The textures go as follows: A, B, D, E, G, H, I, M, N, O, P, R, S, T, U, Y, and an exclamation point.

This pack adds C, F, J, K, L, Q, V, X, Z, and a blank space character.

Here's how it works.

The alphabet texture set in the original game is located in data/game_6.szs, inside of the 'timg' folder.
The actual animation file for controlling what letters the animation displays is in the 'scrn' folder, named 'big_tx_1.blo'.

Normally, I would use a tool like blojob to edit this file, but that tool does not detect dependencies correctly, so the textures won't load.
So, I've been doing it manually with a hex editor. Personally, I use HxD.

Here are the offsets for big_tx_1.blo:

GO!:
0x6F-0x0A - big_tx_g.bti
0xB7-0xC2 - big_tx_o.bti
0xFF-0x10A - big_tx_ex.bti

SHINE!:
0x14B-0x156 - big_tx_s.bti
0x193-0x19D - big_tx_h.bti
0x1DA-0x1E6 - big_tx_i.bti
0x223-0x22E - big_tx_n.bti
0x26B-0x276 - big_tx_e.bti
0x2B3-0x2BF - big_tx_ex.bti

TOO BAD!:
0x2FF-0x30A - big_tx_t.bti
0x347-0x352 - big_tx_o.bti
0x38F-0x39A - big_tx_o.bti
0x3D7-0x3E2 - big_tx_b.bti
0x41F-0x42A - big_tx_a.bti
0x467-0x472 - big_tx_d.bti
0x4AF-0x4BB - big_tx_ex.bti

READY:
0x4FB-0x506 - big_tx_r.bti
0x543-0x54E - big_tx_e.bti
0x58B-0x596 - big_tx_a.bti
0x5D3-0x5DE - big_tx_d.bti
0x61B-0x626 - big_tx_y.bti

In the hex editor, you can't change the file size without the game crashing.
You can change what the animation says by changing the letter in big_tx_*.bti.
If you want to replace an exclamation point with a letter, you have to copy and rename whichever letter you want to have one extra character for padding.
What I usually do is copy the letter texture and add a character. For example, big_tx_dd.bti.

Super Mario Sunshine's ARAM is already tightly packed, so be sure only to copy textures you need delete extra textures you don't need.
If you put too many textures into game_6, it takes up too much memory, and the the game spits out some hilariously broken results.
Adding all of the textures from this pack will not make the game crash on its own. I have tested it many times. It only started becoming unstable when I added like 20 more textures into it.