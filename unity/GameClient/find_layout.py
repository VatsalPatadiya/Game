def check_layout():
    # We need 5 layers.
    # L4: 1x1 = 1 (Odd)
    # L3: 2x2 = 4 (Even)
    # L2: Odd x Odd
    # L1: Even x Even
    # L0: Odd x Odd
    # Sum must be 144.
    for l2w in range(3, 11, 2):
        for l2h in range(3, 11, 2):
            for l1w in range(l2w+1, 15, 2):
                for l1h in range(l2h+1, 15, 2):
                    for l0w in range(l1w+1, 16, 2):
                        for l0h in range(l1h+1, 16, 2):
                            # The base layer usually has holes or wings.
                            # Standard base is 12x8? 12 is Even! But L0 must be Odd?
                            # Wait, if L0 is 13x7?
                            pass
