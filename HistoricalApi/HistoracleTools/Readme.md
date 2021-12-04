When doing 'dumb' Request/Response comparison by endpoint, 
responses can be different simply because another call modified the object

e.g.
Get Order 1368 Order.Status = A
Get Order 1375 Order.Status = B
some other call (presumably) modified Status between the two.
The logical distance between the two Get's really shouldn't be different at all when taking into account
the expect Status...
If we kept track of mutations, we could possibly account for these changes and compute a zero distance.

BUGS
trans probs: 44->44, 37/36=1.0277777777777777, 4/3=1.3333333333333333, 0.3055555555555556
